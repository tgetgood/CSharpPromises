using System;
using Promise;

namespace Examples
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			Promise<string> hello = CreateSimplePromise();
			hello.success(mesg => Console.WriteLine(mesg));
		}

		public static Promise<string> CreateSimplePromise()
		{
			Action<Action<PromiseError, string>> constructor = cb => 
			{
				cb(null, "Hello World");
			};

			return new Promise<string>(constructor);
		}

	}
}
