using System;
using System.Collections.Generic;

namespace Promise
{
	public abstract class Promise
	{
        /// <summary>
        /// Takes a promise of a promise and returns the promised promise.
        /// </summary>
		public static Promise<S> join<S>(Promise<Promise<S>> upper)
		{
			Action<Action<PromiseError, S>> wrap = cb =>
			{
				upper.success((Promise<S> lower) =>
                {
                    lower.success((S t) =>
                    {
                        cb(null, t);
                    });
                    lower.fail(s =>
                    {
                        cb(s, default(S));
                    });
                });
				upper.fail(s =>
                {
                    cb(s, default(S));
                });
			};
			return new Promise<S>(wrap);
		}
        /// <summary>
        /// Converts a list of promises into a promise of a list.
        /// </summary>
		public static Promise<List<S>> invert<S>(List<Promise<S>> promises, bool dropFailures = false)
        // I can't remember the proper name for this kind of functor... If you do please rename. 
		{
			Action<Action<PromiseError, List<S>>> wrap = cb =>
			{
				object locker = new object();
				List<S> list = new List<S>();
				List<PromiseError> failed = new List<PromiseError>();
				Action checkDone = () => {
					if (!dropFailures && failed.Count > 0) {
						cb(failed[0], null);
					} else if (failed.Count + list.Count == promises.Count) {
						cb(null, list);
					}
				};

				promises.ForEach(p =>
                {
                    p.success(val =>
                    {
			            lock (locker)
			            {
			              list.Add(val);
			              checkDone();
			            }
			        });
	                p.fail(e => {
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

			try {
				cb(construct);
			} catch (Exception e) {
				construct(e, default(T));
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

	    public void success(Action<T> cb)
		{
			lock (mutex) {
                if (_completed)
                {
                    if (_succeeded)
                        cb(_val);
                }
                else
                    _onSuccess.Add(cb);
			}
		}

	    public void fail(Action<PromiseError> cb)
		{
			lock (mutex) {
                if (_completed)
                {
                    if (!_succeeded)
                        cb(_err);
                }
                else
                    _onFail.Add(cb);
			}
		}

	    private void construct(PromiseError err, T val)
		{
			if (_completed)
				return;

			_completed = true;
			// Looks a bit too much like Node...
			if (err != null)
			{
				_err = err;
				triggerFailure();
			}
			else 
			{
				_val = val;
				triggerSuccess();
			}
		}

	    private void triggerFailure()
		{
			lock (mutex)
			{
				_succeeded = false;
				_onFail.ForEach(x => x(_err));
				clean();
			}
		}

	    /// <summary>
	    /// Calls success callbacks
	    /// N.B.: The order in which success callbacks are called should be assumed indeterminate.
	    /// </summary>
	    private void triggerSuccess()
		{
			lock (mutex)
			{
				_succeeded = true;
				_onSuccess.ForEach(x => x(_val));
				clean();
			}
		}

		/// <summary>
		/// Once a promise has a value, the callback queues can never again be populated. 
		/// </summary>
	    private void clean()
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
				this.fail(err => 
		        {
			        try 
			        {
			            Promise<T> rec = f(err);
			            if (rec == null)
			              cb(err, null);
			            else
			              cb(null, rec);
			        } 
			        catch(Exception ex) {
			            cb(ex, null);
			        }
			    });

				this.success(s => cb(null, new Promise<T>(ncb => ncb(null, s))));
			};

			return join(new Promise<Promise<T>>(wrap));
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

				this.fail(s => cb(s, null));
				that.fail(s => cb(s, null));

				this.success((T t) =>
                {
                    _this.update(t);
                    if (_that.has)
                        cb(null, new Pair<T, S>(_this.val, _that.val));
                });

				that.success((S s) =>
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
				this.fail((err) => cb(err, default(S)));
				this.success((val) =>
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
			return join(this.map(conv));
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

		public readonly string message;
		public readonly Exception ex;

		public PromiseError(string mesg)
		{
			ex = new Exception(mesg);
			message = mesg;
		}

		public PromiseError(Exception ex)
		{
			this.ex = ex;
			message = ex.Message;
		}

		public override string ToString()
		{
			return message;
		}
	}

    public class Pair<T, S>
    {
        protected T _first;
        protected S _second;

        public T first { get { return _first; } }
        public S second { get { return _second; } }

        public Pair(T t, S s)
        {
            _first = t;
            _second = s;
        }
    }
}
