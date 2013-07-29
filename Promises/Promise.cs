using System;
using System.Collections.Generic;
using System.Linq;

namespace Promises
{
	public abstract class Promise
	{
		/// <summary>
		/// Takes a promise of a promise and returns the promised promise.
		/// </summary>
		public static Promise<T> Join<T>(Promise<Promise<T>> upper)
		{
			Action<Action<PromiseError, T>> wrap = (cb) =>
			{
				upper.Success((Promise<T> lower) =>
				{
					lower.Success((value) =>
					{
						cb(null, value);
					});
					lower.Fail(error =>
					{
						cb(error, default(T));
					});
				});
				upper.Fail(error =>
				{
					cb(error, default(T));
				});
			};
			return new Promise<T>(wrap);
		}
		/// <summary>
		/// Converts a list of promises into a promise of a list.
		/// </summary>
		/// <remarks>I can't remember the proper name for this kind of functor... If you do please rename.</remarks>
		public static Promise<IList<T>> Invert<T>(IEnumerable<Promise<T>> promises, bool dropFailures = false)
		{
			Action<Action<PromiseError, IList<T>>> wrap = (cb) =>
			{
				int promisesCount = promises.Count();
				object locker = new object();
				IList<T> list = new List<T>();
				IList<PromiseError> failed = new List<PromiseError>();
				Action checkDone = () =>
				{
					if (!dropFailures && failed.Count > 0)
					{
						cb(failed[0], null);
					}
					else if (failed.Count + list.Count == promisesCount)
					{
						cb(null, list);
					}
				};

				foreach (var promise in promises)
				{
					promise.Success(value =>
					{
						lock (locker)
						{
							list.Add(value);
							checkDone();
						}
					});
					promise.Fail(error =>
					{
						lock (locker)
						{
							failed.Add(error);
							checkDone();
						}
					});
				}
			};
			return new Promise<IList<T>>(wrap);
		}
	}

	public class Promise<T> : Promise
	{
		//C# 5.0 has the "await" keyword which would let us pretend to block for the value...

		protected bool _completed = false;
		protected bool _succeeded = false;
		protected T _value;
		protected PromiseError _error;
		private object mutex = new object();
		protected IList<Action<T>> _onSuccess;
		protected IList<Action<PromiseError>> _onFail;

		public Promise(T success)
		{
			_completed = true;
			_succeeded = true;
			_value = success;
		}

		public Promise(PromiseError error)
		{
			_completed = true;
			_succeeded = false;
			_error = error;
		}

		public Promise(Action<Action<PromiseError, T>> callback)
		{
			_onSuccess = new List<Action<T>>();
			_onFail = new List<Action<PromiseError>>();

			try
			{
				callback(Construct);
			}
			catch (Exception ex)
			{
				Construct(ex, default(T));
			}
		}

		#region promise wrappers

		public static implicit operator Promise<T>(PromiseError error)
		{
			return new Promise<T>(cb => cb(error, default(T)));
		}

		/// <summary>
		/// Covariance should just work. What good are the interfaces? 
		/// </summary>
		public static implicit operator Promise<object>(Promise<T> promise)
		{
			return promise.Map(t => t as object);
		}

		#endregion

		#region promise logic

		public void Success(Action<T> callback)
		{
			lock (mutex)
			{
				if (_completed)
				{
					if (_succeeded)
						callback(_value);
				}
				else
					_onSuccess.Add(callback);
			}
		}

		public void Fail(Action<PromiseError> callback)
		{
			lock (mutex)
			{
				if (_completed)
				{
					if (!_succeeded)
						callback(_error);
				}
				else
					_onFail.Add(callback);
			}
		}

		private void Construct(PromiseError error, T value)
		{
			if (_completed)
				return;

			_completed = true;
			// Looks a bit too much like Node...
			if (error != null)
			{
				_error = error;
				TriggerFailure();
			}
			else
			{
				_value = value;
				TriggerSuccess();
			}
		}

		private void TriggerFailure()
		{
			lock (mutex)
			{
				_succeeded = false;
				foreach (var callback in _onFail)
				{
					callback(_error);
				}
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
				foreach (var callback in _onSuccess)
				{
					callback(_value);
				}
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
		public Promise<T> Recover(Func<PromiseError, Promise<T>> f)
		{
			Action<Action<PromiseError, Promise<T>>> wrap = (cb) =>
			{
				this.Fail(error =>
				{
					try
					{
						Promise<T> recovered = f(error);
						if (recovered == null)
							cb(error, null);
						else
							cb(null, recovered);
					}
					catch (Exception ex)
					{
						cb(ex, null);
					}
				});

				this.Success(value => cb(null, new Promise<T>(ncb => ncb(null, value))));
			};

			return Join(new Promise<Promise<T>>(wrap));
		}

		public Promise<T> Recover(Func<PromiseError, T> f)
		{
			return Recover(error => new Promise<T>(cb => cb(null, f(error))));
		}

		/// <summary>
		///  Helper class for Combine.
		/// </summary>
		private class Wrapper<K>
		{
			public K Value;
			public bool HasValue = false;

			public void Update(K value)
			{
				this.Value = value;
				this.HasValue = true;
			}
		}

		/// <summary>
		/// Combine two promises of different types into one promise of a tuple.
		/// </summary>
		public Promise<Tuple<T, S>> Combine<S>(Promise<S> that)
		{
			Action<Action<PromiseError, Tuple<T, S>>> wrap = (cb) =>
			{
				Wrapper<T> _this = new Wrapper<T>();
				Wrapper<S> _that = new Wrapper<S>();

				this.Fail(s => cb(s, null));
				that.Fail(s => cb(s, null));

				this.Success((T valueT) =>
				{
					_this.Update(valueT);
					if (_that.HasValue)
						cb(null, new Tuple<T, S>(_this.Value, _that.Value));
				});

				that.Success((S valueS) =>
				{
					_that.Update(valueS);
					if (_this.HasValue)
						cb(null, new Tuple<T, S>(_this.Value, _that.Value));
				});
			};

			return new Promise<Tuple<T, S>>(wrap);
		}

		/// <summary>
		/// Converts a promise of a T into a promise of an S given a function T -> S
		/// </summary>
		public Promise<S> Map<S>(Func<T, S> convert)
		{
			Action<Action<PromiseError, S>> wrap = (cb) =>
			{
				this.Fail((error) => cb(error, default(S)));
				this.Success((value) =>
				{
					try
					{
						S newValue = convert(value);
						cb(null, newValue);
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
		/// Converts a promise of a T into a promise of an S given a function T -> Promise&lt;S&gt;
		/// aka bind
		/// </summary>
		public Promise<S> FlatMap<S>(Func<T, Promise<S>> convert)
		{
			return Join(this.Map(convert));
		}

		#endregion
	}
}
