using System;
using Promise;

namespace Examples
{
	class MainClass
	{
		/// <summary>
		/// Not a very useful example, just need a main class to compile and run the tests.  
		/// </summary>
		public static void Main(string[] args)
		{
			Promise<string> hello = CreateSimplePromise();
			hello.Success(mesg => Console.WriteLine(mesg));
		}

		/// <summary>
		/// Create a simple synchronous promise.
		/// </summary>
		public static Promise<string> CreateSimplePromise()
		{
			// The promise constructor takes a function that takes a callback. 
			// By passing it's own contruction function as the required callback, 
			// the promise can encapsulate the results returned via the callback.
			Action<Action<PromiseError, string>> constructor = cb =>
			{
				cb(null, "Hello World");
			};

			return new Promise<string>(constructor);
		}

	}
}
