using Promises;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples
{
	class Program
	{

		/// <summary>
		/// Not a very useful example, just need a main class to compile and run the tests.  
		/// </summary>
		public static void Main(string[] args)
		{
			Promise<string> hello = CreateSimplePromise();
			hello.Success(mesg => Console.WriteLine(mesg));
			Console.ReadLine();
		}

		/// <summary>
		/// Create a simple synchronous promise.
		/// </summary>
		public static Promise<string> CreateSimplePromise()
		{
			// The promise constructor takes a function that takes a callback. 
			// By passing it's own contruction function as the required callback, 
			// the promise can encapsulate the results returned via the callback.
			Action<Action<PromiseError, string>> constructor = (cb) =>
			{
				cb(null, "Hello World");
			};

			return new Promise<string>(constructor);
		}

	}
}
