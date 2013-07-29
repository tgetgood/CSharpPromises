using System;
using System.Collections.Generic;

namespace Promise
{
	public abstract class Promise
	{
		/// <summary>
		/// Takes a promise of a promise and returns the promised promise.
		/// </summary>
		public static Promise<S> Join<S>(Promise<Promise<S>> upper)
		{
			Action<Action<PromiseError, S>> wrap = cb =>
			{
				upper.Success((Promise<S> lower) =>
				{
					lower.Success((S t) =>
					{
						cb(null, t);
					});
					lower.Fail(s =>
					{
						cb(s, default(S));
					});
				});
				upper.Fail(s =>
				{
					cb(s, default(S));
				});
			};
			return new Promise<S>(wrap);
		}
		/// <summary>
		/// Converts a list of promises into a promise of a list.
		/// </summary>
		/// <remarks>I can't remember the proper name for this kind of functor... If you do please rename.</remarks>
		public static Promise<List<S>> Invert<S>(List<Promise<S>> promises, bool dropFailures = false)
		{
			Action<Action<PromiseError, List<S>>> wrap = cb =>
			{
				object locker = new object();
				List<S> list = new List<S>();
				List<PromiseError> failed = new List<PromiseError>();
				Action checkDone = () =>
				{
					if (!dropFailures && failed.Count > 0)
					{
						cb(failed[0], null);
					}
					else if (failed.Count + list.Count == promises.Count)
					{
						cb(null, list);
					}
				};

				promises.ForEach(p =>
				{
					p.Success(val =>
					{
						lock (locker)
						{
							list.Add(val);
							checkDone();
						}
					});
					p.Fail(e =>
					{
						lock (locker)
						{
							failed.Add(e);
							checkDone();
						}
					});
				});
			};
			return new Promise<List<S>>(wrap);
		}
	}

	public class Promise<T> : Promise
	{
		//C# 5.0 has the "await" keyword which would let us pretend to block for the value...

		protected bool _completed = false;
		protected bool _succeeded = false;
		protected T _val;
		protected PromiseError _err;
		private object mutex = new object();
		protected List<Action<T>> _onSuccess;
		protected List<Action<PromiseError>> _onFail;

		public Promise(T success)
		{
			_completed = true;
			_succeeded = true;
			_val = success;
		}

		public Promise(PromiseError err)
		{
			_completed = true;
			_succeeded = false;
			_err = err;
		}

		public Promise(Action<Action<PromiseError, T>> cb)
		{
			_onSuccess = new List<Action<T>>();
			_onFail = new List<Action<PromiseError>>();

			try
			{
				cb(Construct);
			}
			catch (Exception e)
			{
				Construct(e, default(T));
			}
		}

		#region promise wrappers

		public static implicit operator Promise<T>(PromiseError fail)
		{
			return new Promise<T>(cb => cb(fail, default(T)));
		}

		/// <summary>
		/// Covariance should just work. What good are the interfaces? 
		/// </summary>
		public static implicit operator Promise<object>(Promise<T> promise)
		{
			return promise.map(t => t as object);
		}

		#endregion

		#region promise logic

		public void Success(Action<T> cb)
		{
			lock (mutex)
			{
				if (_completed)
				{
					if (_succeeded)
						cb(_val);
				}
				else
					_onSuccess.Add(cb);
			}
		}

		public void Fail(Action<PromiseError> cb)
		{
			lock (mutex)
			{
				if (_completed)
				{
					if (!_succeeded)
						cb(_err);
				}
				else
					_onFail.Add(cb);
			}
		}

		private void Construct(PromiseError err, T val)
		{
			if (_completed)
				return;

			_completed = true;
			// Looks a bit too much like Node...
			if (err != null)
			{
				_err = err;
				TriggerFailure();
			}
			else
			{
				_val = val;
				TriggerSuccess();
			}
		}

		private void TriggerFailure()
		{
			lock (mutex)
			{
				_succeeded = false;
				_onFail.ForEach(x => x(_err));
				Clean();
			}
		}

		/// <summary>
		/// Calls success callbacks
		/// N.B.: The order in which success callbacks are called should be assumed indeterminate.
		/// </summary>
		private void TriggerSuccess()
		{
			lock (mutex)
			{
				_succeeded = true;
				_onSuccess.ForEach(x => x(_val));
				Clean();
			}
		}

		/// <summary>
		/// Once a promise has a value, the callback queues can never again be populated. 
		/// </summary>
		private void Clean()
		{
			// These can never be used again, so we may as well save a bit of memory.
			_onSuccess = null;
			_onFail = null;
		}

		#endregion

		#region Higher order function

		/// <summary>
		/// If the promise fails, apply f to the error and try to recover a value.
		/// If f throws an exception the recovered promise will also fail.
		/// </summary>
		public Promise<T> recover(Func<PromiseError, Promise<T>> f)
		{
			Action<Action<PromiseError, Promise<T>>> wrap = cb =>
			{
				this.Fail(err =>
				{
					try
					{
						Promise<T> rec = f(err);
						if (rec == null)
							cb(err, null);
						else
							cb(null, rec);
					}
					catch (Exception ex)
					{
						cb(ex, null);
					}
				});

				this.Success(s => cb(null, new Promise<T>(ncb => ncb(null, s))));
			};

			return Join(new Promise<Promise<T>>(wrap));
		}

		public Promise<T> recover(Func<PromiseError, T> f)
		{
			return recover(e => new Promise<T>(cb => cb(null, f(e))));
		}

		/// <summary>
		///  Helper class for combine.
		/// </summary>
		private class Wrapper<K>
		{
			public K val;
			public bool has = false;

			public void update(K t)
			{
				val = t;
				has = true;
			}
		}

		/// <summary>
		/// Combine two promises of different types into one promise of a pair.
		/// </summary>
		public Promise<Pair<T, S>> combine<S>(Promise<S> that)
		{
			Action<Action<PromiseError, Pair<T, S>>> wrap = (cb) =>
			{
				Wrapper<T> _this = new Wrapper<T>();
				Wrapper<S> _that = new Wrapper<S>();

				this.Fail(s => cb(s, null));
				that.Fail(s => cb(s, null));

				this.Success((T t) =>
				{
					_this.update(t);
					if (_that.has)
						cb(null, new Pair<T, S>(_this.val, _that.val));
				});

				that.Success((S s) =>
				{
					_that.update(s);
					if (_this.has)
						cb(null, new Pair<T, S>(_this.val, _that.val));
				});
			};

			return new Promise<Pair<T, S>>(wrap);
		}

		/// <summary>
		/// Converts a promise of a T into a promise of an S given a function T -> S
		/// </summary>
		public Promise<S> map<S>(Func<T, S> convert)
		{
			Action<Action<PromiseError, S>> wrap = cb =>
			{
				this.Fail((err) => cb(err, default(S)));
				this.Success((val) =>
				{
					try
					{
						S newVal = convert(val);
						cb(null, newVal);
					}
					catch (Exception e)
					{
						cb(new PromiseError(e), default(S));
					}
				});
			};
			return new Promise<S>(wrap);
		}

		/// <summary>
		/// Converts a promise of a T into a promise of an S given a function T -> Promise<S>
		/// aka bind
		/// </summary>
		public Promise<S> flatMap<S>(Func<T, Promise<S>> conv)
		{
			return Join(this.map(conv));
		}

		#endregion
	}

	public class PromiseError
	{
		public static implicit operator PromiseError(string mesg)
		{
			return new PromiseError(mesg);
		}

		public static implicit operator PromiseError(Exception ex)
		{
			return new PromiseError(ex);
		}

		public readonly string Message;
		public readonly Exception Ex;

		public PromiseError(string mesg)
		{
			Ex = new Exception(mesg);
			Message = mesg;
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

	public class Pair<T, S>
	{
		public T First { get; protected set; }
		public S Second { get; protected set; }

		public Pair(T t, S s)
		{
			First = t;
			Second = s;
		}
	}
}
