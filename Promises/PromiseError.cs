using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Promises
{
	public class PromiseError
	{
		public static implicit operator PromiseError(string message)
		{
			return new PromiseError(message);
		}

		public static implicit operator PromiseError(Exception ex)
		{
			return new PromiseError(ex);
		}

		public readonly string Message;
		public readonly Exception Ex;

		public PromiseError(string message)
		{
			Ex = new Exception(message);
			Message = message;
		}

		public PromiseError(Exception ex)
		{
			this.Ex = ex;
			Message = ex.Message;
		}

		public override string ToString()
		{
			return Message;
		}
	}
}
