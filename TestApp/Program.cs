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
			var person4 = person3.Clone();
			var person5 = person1.Clone();

			person3.Name = "Jacob";

			Console.WriteLine(String.Join("\n", new Object[] { person1, person2, person3, person4, person5 }));

			person1.Name = "Han";

			Console.WriteLine(String.Join("\n", new Object[] { person1, person2, person3, person4, person5 }));
		}
	}
}