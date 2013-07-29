using System;
using NUnit.Framework;

namespace Promise
{
	[TestFixture]
	public class PromiseTests
	{
		[Test]
		public void TestBasicPromise()
		{
			Promise<int> p1 = new Promise<int>(cb => cb(null, 5));
			bool test = false;
			p1.Success(s => test = true);
			p1.Success(s => Assert.AreEqual(s, 5));
			Assert.IsTrue(test);
		}

		[Test]
		public void TestCombinePromise()
		{
			Promise<int> p1 = new Promise<int>(cb => cb(null, 5));
			Promise<string> p2 = new Promise<string>(cb => cb(null, "some string"));

			var combo = p1.Combine(p2);

			bool test = false;

			combo.Success(p =>
			{
				Assert.AreEqual(p.First, 5);
				Assert.AreEqual(p.Second, "some string");
			});

			combo.Success(s => test = true);

			Assert.IsTrue(test);
		}

		[Test]
		public void TestMapPromise()
		{
			Promise<int> p1 = new Promise<int>(cb => cb(null, 5));
			Promise<string> p2 = p1.Map(x => x.ToString());

			p2.Success(s => Assert.AreEqual(s, "5"));

			bool test = false;

			p2.Success(s => test = true);

			Assert.IsTrue(test);
		}

		[Test]
		public void TestJoin()
		{
			Promise<Promise<string>> p = new Promise<Promise<string>>(cb => cb(null, new Promise<string>(cb2 => cb2(null, "gotcha"))));

			p.Success(s => Assert.AreNotEqual(s, "gotcha"));
			p.Success(s => s.Success(y => Assert.AreEqual(y, "gotcha")));

			var p2 = Promise.Join(p);

			p2.Success(s => Assert.AreEqual(s, "gotcha"));

			bool test = false;

			p2.Success(x => test = true);

			Assert.IsTrue(test);
		}

		[Test]
		public void TestFlatMap()
		{
			Promise<int> p1 = new Promise<int>(cb => cb(null, 5));
			Promise<string> p2 = p1.FlatMap(i => new Promise<string>(cb => cb(null, i.ToString())));

			p2.Success(s => Assert.AreEqual(s, "5"));

			bool test = false;

			p2.Success(x => test = true);

			Assert.IsTrue(test);
		}

		[Test]
		public void TestcompoundFunc()
		{
			Promise<string> suf = new Promise<string>(cb => cb(null, "second"));

			Promise<string> full = suf.FlatMap(x => convert("first", x));

			full.Success(x => Assert.AreEqual(x, "first, second"));

			bool test = false;

			full.Success(x => test = true);

			Assert.IsTrue(test);
		}

		public Promise<string> convert(string prefix, string suffix)
		{
			return new Promise<string>(cb => cb(null, prefix + ", " + suffix));
		}

		[Test]
		public void TestFailure()
		{
			Promise<long> p = new Promise<long>(cb => cb("uh oh", default(long)));

			bool test = false;

			p.Success(x => test = true);

			Assert.IsFalse(test);

			p.Fail(s => test = true);

			Assert.IsTrue(test);

			var p2 = p.FlatMap(l => new Promise<int>(cb => cb(null, 3)));

			test = false;

			p2.Success(x => test = true);

			Assert.IsFalse(test);

			p2.Fail(s => test = true);

			Assert.IsTrue(test);
		}
	}
}
