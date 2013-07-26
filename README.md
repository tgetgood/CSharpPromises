C# Promises
===========

A simple library implementing non blocking promises in C#. 

Use 
=====

Promises encapsulate values that may or may not be computed yet. 

Promises have a distinct advantage over callbacks in async programming in that they are objects. That means that they can be passed into and returned from functions, manipulated, and cached.

Whereas a callback or delegate will return data only once, the data inside a promise can be accessed repeatedly by multiple functions across the entire lifetime of the promise object.

Basic Usage
-----

Since a promise contains a result that may or may not have been computed yet, there's always a chance that the computation will fail. Thus promises have two functions to extact their result.

 promise.fail((PromiseError error) => { // Do something with the error });

and

 promise.success((T value) => {// do something with the value});

A PromiseError object is simply a wrapper for an error message or an exception that caused the promised computation to fail. 

Some might ask "If you have to give a callback (delegate) to a promise in order to get the value out of it, then why are they better than just using callbacks?". The simple answer is that a promise keeps its value and will pass it to as many callbacks as you like. A better answer is all of the cool stuff you can do with promises in the next section.

Manipulating Promises
---------------------

Say you have a library which makes web requests and returns Promise<HTMLResponse> objects. If you want to get just the status code, you could just let the caller add a callback to be run on success of the promise which extracts what they want, but then you're returning way more than the caller needs. Furthermore, what if you want to hide your internal HTMLResponse objects so that external classes don't have to deal with them? 

Let's assume that our HTMLResponse object has a .GetStatus property. Then the function 
 res => res.GetStatus
converts an HTMLResponse into a StatusCode. Wouldn't it be great if we could use this same function to convert a Promise<HTMLResponse> into a Promise<StatusCode>? Well we can't directly, because the data is wrapped up in the promise, but promises have a method called `map` which will accomplish exactly this for us. Thus if promise is a Promise<HTMLResponse>,
 promise.map(res => res.GetCode);
will return a Promise<StatusCode>. Easy no?

Now our web client library can work internally with promises of objects without ever exposing those objects to the outside world. 

There are lots of more complex cases that come up as you start to use promises. For instance what if you have a function `StringMe` that given an `int` returns a `Promise<string>`. Now suppose that you only have a `Promise<int>`, if you use map like so:
 pInt.map(StringMe);
then you end up with a `Promise<Promise<string>>`.

Fortunately there is help to be had. The `join` function takes a promise of a promise and returns the promised promise. What this means is that we can get our `Promise<string>` like so:
 Promise.join(pInt.map(StringMe));

 This case is in fact so common that there's a special function for it called `flatMap`. The above is equivalent to:
  pInt.flatMap(StringMe);

There are more helpful functions to do things like combine several promises into one, and even to convert a list of promises (of a single type) into a promise of a list. These are documented in the code and straight forward to use if you understand the above examples. 

Creating Promises
-----------------

Creating promises looks a little odd at first, but if you think about what it means to encapsulate the result of an async computation, I hope you'll see why they were made this way. 

The constructor for a `Promise<T>` takes a single `Action<Action<PromiseError, T>>`. That is it takes a function that expects a callback. By passing its own construction function as the expected callback, the promise can intercept the returned data and store it before passing it on to any waiting success functions. 

Because a promise may fail, I've followed the convention (most evangelised by Node.js) of sending both possible results to a single callback. I'll show below how to make this work cleanly with async libs that use separate success and failure delegates. 

Assume we have a web client that takes a callback and always returns a Response object. To create a promise of HTML text you could do the following:

 Action<Action<PromiseError, string>> constructor = cb => 
 {
	Action<Response> webCb = res => 
	{
		if (res.StatusCode != 200)
			cb(new PromiseError("Invalid Status Code"), null);
		else if // Check more conditions

		else
			cb(null, res.HTML);
	};

	 WebClient.Get(req, webCb); // req comes from somewhere above.
 }

 return new Promise<string>(cb);

So we see that if the error argument is non null, the promise fails, otherwise it succeeds. Similarly if our web client has delegates `RequestSucceeded` and `RequestFailed` then we can write it as follows:

 Action<Action<PromiseError, string>> constructor = cb => 
 {
	WebClient.RequestSucceeded += res => cb(null, res.HTML);
	WebClient.RequestFailed += err => cb(new PromiseError(err.ToString()), null);

	 WebClient.Get(req); // req comes from somewhere above.
 }

 return new Promise<string>(cb);

License
=======

Author Info
===========

Thomas Getgood <thomas@tastefilter.com>
