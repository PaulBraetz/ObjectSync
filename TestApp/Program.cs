using System.ComponentModel;

namespace TestApp
{
	public class Program
	{
		static void Main(string[] args)
		{
			var person1 = new Person();
			var person2 = person1.Clone();
			var person3 = person2.Clone();

			Console.WriteLine(String.Join("\n", new Object[] { person1, person2, person3 }));

			person3.Name = "Jacob";

			Console.WriteLine(String.Join("\n", new Object[] { person1, person2, person3 }));

			person1.Age = 44;

			Console.WriteLine(String.Join("\n", new Object[] { person1, person2, person3 }));
		}
	}
}