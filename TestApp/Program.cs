namespace TestApp
{
	public class Program
	{
		static void Main(string[] args)
		{
			var person0 = new Person("Haris");
			var person1 = new Person("Mike");
			var person2 = new Person(person1.Id);
			var person3 = new Person(person2.Id);
			print();
			person3.Name = "Jacob";
			print();
			person3.Desynchronize();
			print();
			person3.Name = "Trevor";
			print();
			person2.Name = "Steve";
			print();
			person2.Synchronize(person0.Id);
			print();
			person2.Name = "Francis";
			print();

			void print()
			{
				Console.WriteLine(String.Join("\n", new Object[] {person0, person1, person2, person3, String.Empty }));
			}
		}
	}
}