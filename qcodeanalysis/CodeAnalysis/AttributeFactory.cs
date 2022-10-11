using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Xml.Linq;

namespace RhoMicro.CodeAnalysis.Attributes
{
	internal abstract class AttributeFactory<T> : IAttributeFactory<T>
	{
		public static IAttributeFactory<T> Create()
		{
			var collection = new AttributeFactoryCollection<T>();

			var type = typeof(T);
			var properties = type.GetProperties().Where(p => p.CanWrite).ToArray();
			var constructors = type.GetConstructors().Where(c => !c.IsStatic).ToArray();

			for (var ctorIndex = 0; ctorIndex < constructors.Length; ctorIndex++)
			{
				var constructor = constructors[ctorIndex];

				var parameters = new ParameterExpression[2]
				{
							Expression.Parameter(typeof(AttributeSyntax), "attributeData"),
							Expression.Parameter(typeof(SemanticModel), "semanticModel"),
				};
				var canBuildStrategy = getCanBuildStrategy();
				var buildStrategy = getBuildStrategy();
				var factory = Create(canBuildStrategy, buildStrategy);

				collection.Add(factory);

				Func<AttributeSyntax, SemanticModel, T> getBuildStrategy()
				{
					var canBuildTest = getCanBuildTest();
					var ifTrue = getBuildExpr();
					var ifFalse = getThrowExpr($"Cannot build {typeof(T)} using the attribute syntax and semantic model provided.");
					var body = Expression.Condition(canBuildTest, ifTrue, ifFalse);

					var lambda = Expression.Lambda(body, parameters);
					var strategy = (Func<AttributeSyntax, SemanticModel, T>)lambda.Compile();

					return strategy;

					Expression getBuildExpr()
					{
						var ctorParams = constructor.GetParameters().ToArray();

						var blockVariables = new List<ParameterExpression>();
						var blockExpressions = new List<Expression>();

						for (var i = 0; i < ctorParams.Length; i++)
						{
							var parameter = ctorParams[i];

							var tryParseMethod = getTryParseMethod(parameter.ParameterType);
							var outValue = Expression.Parameter(parameter.ParameterType, $"argumentValue_{parameter.Name}");
							var callExpr = Expression.Call(null, tryParseMethod, parameters[0], parameters[1], outValue, Expression.Constant(i), Expression.Convert(Expression.Constant(null), typeof(String)), Expression.Constant(parameter.Name));

							Expression noArgReactionExpr = null;
							if (parameter.HasDefaultValue)
							{
								noArgReactionExpr = Expression.Assign(outValue, Expression.Convert(Expression.Constant(parameter.DefaultValue), parameter.ParameterType));
							}
							else
							{
								noArgReactionExpr = getThrowExpr($"Missing argument for {parameter.Name} of type {parameter.ParameterType} encountered while attempting to construct instance of type {typeof(T)}.");
							}

							var paramAssignmentExpr = Expression.IfThen(Expression.Not(callExpr), noArgReactionExpr);

							blockVariables.Add(outValue);
							blockExpressions.Add(paramAssignmentExpr);
						}

						var newInstanceExpr = Expression.Variable(type, "instance");
						var newExpr = Expression.New(constructor, blockVariables);
						var newInstanceAssignmentExpr = Expression.Assign(newInstanceExpr, newExpr);

						blockVariables.Add(newInstanceExpr);
						blockExpressions.Add(newInstanceAssignmentExpr);

						for (var i = 0; i < properties.Length; i++)
						{
							var property = properties[i];

							var tryParseMethod = getTryParseMethod(property.PropertyType);
							var outValue = Expression.Parameter(property.PropertyType, $"propertyValue_{property.Name}");
							var callExpr = Expression.Call(null, tryParseMethod, parameters[0], parameters[1], outValue, Expression.Constant(-1), Expression.Constant(property.Name), Expression.Convert(Expression.Constant(null), typeof(String)));

							var setExpr = Expression.Call(newInstanceExpr, property.SetMethod, outValue);
							var setConditionExpr = Expression.IfThen(callExpr, setExpr);

							blockVariables.Add(outValue);
							blockExpressions.Add(setConditionExpr);
						}

						blockExpressions.Add(newInstanceExpr);

						var block = Expression.Block(blockVariables, blockExpressions);

						return block;

						MethodInfo getTryParseMethod(Type forType)
						{
							var name = forType.IsArray ?
								nameof(Extensions.TryParseArrayArgument) :
								nameof(Extensions.TryParseArgument);
							var constraint = forType.IsArray ? forType.GetElementType() : forType;

							var method = typeof(Extensions).GetMethods()
								.Where(m => m.IsGenericMethod)
								.Select(m => m.MakeGenericMethod(constraint))
								.Single(m =>
								{
									var methodParams = m.GetParameters();
									return m.Name == name &&
										methodParams.Length == 6 &&
										methodParams[0].ParameterType == typeof(AttributeSyntax) &&
										methodParams[1].ParameterType == typeof(SemanticModel) &&
										methodParams[2].ParameterType == forType.MakeByRefType() &&
										methodParams[3].ParameterType == typeof(Int32) &&
										methodParams[4].ParameterType == typeof(String) &&
										methodParams[5].ParameterType == typeof(String);
								});

							return method;
						}
					}
					Expression getThrowExpr(String message)
					{
						var ctorInfo = typeof(InvalidOperationException).GetConstructor(new[] { typeof(String) });
						var ctorParam = Expression.Constant(message);
						var throwExpr = Expression.Throw(Expression.New(ctorInfo, ctorParam));
						var returnExpr = Expression.Convert(Expression.Constant(null), type);
						var throwBlock = Expression.Block(throwExpr, returnExpr);

						return throwBlock;
					}
				}
				Func<AttributeSyntax, SemanticModel, Boolean> getCanBuildStrategy()
				{
					var body = getCanBuildTest();

					var lambda = Expression.Lambda(body, parameters);
					var strategy = (Func<AttributeSyntax, SemanticModel, Boolean>)lambda.Compile();

					return strategy;
				}
				Expression getCanBuildTest()
				{
					var typeExpr = Expression.Constant(type, typeof(Type));
					var getCtorsMethod = typeof(Type).GetMethod(nameof(Type.GetConstructors), new Type[] { });
					var getCtorsCallExpr = Expression.Call(typeExpr, getCtorsMethod);

					var whereMethod = typeof(Enumerable).GetMethods()
						.Where(m => m.Name == nameof(Enumerable.Where))
						.Select(m => m.MakeGenericMethod(typeof(ConstructorInfo)))
						.Single(m =>
						{
							var methodParameters = m.GetParameters();
							var match = methodParameters.Length == 2 &&
										methodParameters[0].ParameterType == typeof(IEnumerable<ConstructorInfo>) &&
										methodParameters[1].ParameterType == typeof(Func<ConstructorInfo, Boolean>);

							return match;
						});
					var predicateParamExpr = Expression.Parameter(typeof(ConstructorInfo), "c");
					var isStaticMethod = typeof(ConstructorInfo).GetProperty(nameof(ConstructorInfo.IsStatic)).GetMethod;
					var predicateExpr = Expression.Lambda(Expression.Not(Expression.Call(predicateParamExpr, isStaticMethod)), predicateParamExpr);
					var whereCallExpr = Expression.Call(null, whereMethod, getCtorsCallExpr, predicateExpr);

					var toArrayMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray)).MakeGenericMethod(typeof(ConstructorInfo));
					var toArrayCall = Expression.Call(null, toArrayMethod, whereCallExpr);

					var ctorIndexExpr = Expression.Constant(ctorIndex);
					var indexAccessExpr = Expression.ArrayIndex(toArrayCall, ctorIndexExpr);

					var matchesMethod = typeof(Extensions).GetMethods()
						.Single(m =>
						{
							var methodParams = m.GetParameters();
							return m.Name == nameof(Extensions.Matches) &&
								methodParams.Length == 2 &&
								methodParams[0].ParameterType == typeof(AttributeSyntax) &&
								methodParams[1].ParameterType == typeof(ConstructorInfo);
						});
					var matchesCall = Expression.Call(null, matchesMethod, parameters[0], indexAccessExpr);

					return matchesCall;
				}
			}

			return collection;
		}
		public static IAttributeFactory<T> Create(Func<AttributeSyntax, SemanticModel, Boolean> canBuildStrategy, Func<AttributeSyntax, SemanticModel, T> buildStrategy)
		{
			return new AttributeFactoryStrategy<T>(canBuildStrategy, buildStrategy);
		}
		public static IAttributeFactory<T> Create(TypeIdentifier typeIdentifier, Func<AttributeSyntax, SemanticModel, T> buildStrategy)
		{
			return new AttributeFactoryStrategy<T>((d, s) => d.IsType(s, typeIdentifier), buildStrategy);
		}

		protected abstract T Build(AttributeSyntax attributeData, SemanticModel semanticModel);
		protected abstract Boolean CanBuild(AttributeSyntax attributeData, SemanticModel semanticModel);
		public Boolean TryBuild(AttributeSyntax attributeData, SemanticModel semanticModel, out T attribute)
		{
			if (CanBuild(attributeData, semanticModel))
			{
				attribute = Build(attributeData, semanticModel);
				return true;
			}

			attribute = default;
			return false;
		}
	}
}
