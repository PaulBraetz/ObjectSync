using System.ComponentModel;

namespace TestApp
{
	public class Program
	{
		static void Main(string[] args)
		{
			var person1 = new Person("Mike", 23);
			var person2 = new Person(person1.Id);
			var person3 = new Person(person2.Id);

			Console.WriteLine(String.Join("\n", new Object[] { person1, person2, person3 }));

			person3.Name = "Jacob";

			Console.WriteLine(String.Join("\n", new Object[] { person1, person2, person3 }));

			person1.Age = 44;

			Console.WriteLine(String.Join("\n", new Object[] { person1, person2, person3 }));
		}
	}
}