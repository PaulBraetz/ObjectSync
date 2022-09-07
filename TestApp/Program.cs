using TestApp.Data.AnotherNamespace;

namespace TestApp
{
	public class Program
	{
		static void Main(string[] args)
		{
			var p0 = new Person("Haris");
			var p1 = new Person("Mike");
			var p2 = new Person(p0);
			var p3 = new Person(p0);
			print($"var {nameof(p0)} = new Person(\"Haris\");\nvar {nameof(p1)} = new Person(\"Mike\");\nvar {nameof(p2)} = new Person(p1);\nvar {nameof(p3)} = new Person(p2);");

			p3.Name = "Jacob";
			print("p3.Name = \"Jacob\";");

			p3.Desynchronize();
			print("p3.Desynchronize();");

			p3.Name = "Trevor";
			print("p3.Name = \"Trevor\";");

			p2.Name = "Steve";
			print("p2.Name = \"Steve\";");

			p2.SynchronizeTo(p1);
			print("p2.SynchronizeTo(p1);");

			p2.Name = "Jacob";
			print("p2.Name = \"Jacob\";");

			void print(String info)
			{
				Console.WriteLine(info);
				Console.WriteLine($"---------------\n{String.Join("\n", new Object[] { $"{nameof(p0)}: {p0}", $"{nameof(p1)}: {p1}", $"{nameof(p2)}: {p2}", $"{nameof(p3)}: {p3}", String.Empty })}");
			}
		}
	}
}