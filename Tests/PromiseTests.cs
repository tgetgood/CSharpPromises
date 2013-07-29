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
			p1.success(s => test = true);
			p1.success(s => Assert.AreEqual(s, 5));
			Assert.IsTrue(test);
		}

		[Test]
		public void TestCombinePromise()
		{
			Promise<int> p1 = new Promise<int>(cb => cb(null, 5));
			Promise<string> p2 = new Promise<string>(cb => cb(null, "some string"));

			var combo = p1.combine(p2);

			bool test = false;

			combo.success(p =>
			{
				Assert.AreEqual(p.first, 5);
				Assert.AreEqual(p.second, "some string");
			});

			combo.success(s => test = true);

			Assert.IsTrue(test);
		}

		[Test]
		public void TestMapPromise()
		{
			Promise<int> p1 = new Promise<int>(cb => cb(null, 5));
			Promise<string> p2 = p1.map(x => x.ToString());

			p2.success(s => Assert.AreEqual(s, "5"));

			bool test = false;

			p2.success(s => test = true);

			Assert.IsTrue(test);
		}

		[Test]
		public void TestJoin()
		{
			Promise<Promise<string>> p = new Promise<Promise<string>>(cb => cb(null, new Promise<string>(cb2 => cb2(null, "gotcha"))));

			p.success(s => Assert.AreNotEqual(s, "gotcha"));
			p.success(s => s.success(y => Assert.AreEqual(y, "gotcha")));

			var p2 = Promise.join(p);

			p2.success(s => Assert.AreEqual(s, "gotcha"));

			bool test = false;

			p2.success(x => test = true);

			Assert.IsTrue(test);
		}

		[Test]
		public void TestFlatMap()
		{
			Promise<int> p1 = new Promise<int>(cb => cb(null, 5));
			Promise<string> p2 = p1.flatMap(i => new Promise<string>(cb => cb(null, i.ToString())));

			p2.success(s => Assert.AreEqual(s, "5"));

			bool test = false;

			p2.success(x => test = true);

			Assert.IsTrue(test);
		}

		[Test]
		public void TestcompoundFunc()
		{
			Promise<string> suf = new Promise<string>(cb => cb(null, "second"));

			Promise<string> full = suf.flatMap(x => convert("first", x));

			full.success(x => Assert.AreEqual(x, "first, second"));

			bool test = false;

			full.success(x => test = true);

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

			p.success(x => test = true);

			Assert.IsFalse(test);

			p.fail(s => test = true);

			Assert.IsTrue(test);

			var p2 = p.flatMap(l => new Promise<int>(cb => cb(null, 3)));

			test = false;

			p2.success(x => test = true);

			Assert.IsFalse(test);

			p2.fail(s => test = true);

			Assert.IsTrue(test);
		}
	}
}
