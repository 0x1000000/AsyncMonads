using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AsyncMonads
{
    internal class BreakException : Exception { }

    [AsyncMethodBuilder(typeof(MaybeExTaskMethodBuilder<>))]
    public class MaybeEx<T> : INotifyCompletion
    {
        private MaybeExResult? _result;

        private Exception _exception;

        internal MaybeEx() { }

        private MaybeEx(MaybeExResult result)
        {
            this._result = result;
            this.IsCompleted = true;
        }

        public bool IsCompleted { get; private set; }

        public void OnCompleted(Action continuation) => continuation();

        public MaybeEx<T> GetAwaiter() => this;

        public MaybeExResultAwaiter GetMaybeResult() => new MaybeExResultAwaiter(this);

        public T GetResult()
        {
            if (this._result.HasValue && this._result.Value.IsNothing)
            {
                throw new BreakException();
            }
            return this.ValidateResult().GetValue();
        }

        internal void SetResult(T result)
        {
            this._result = MaybeExResult.Value(result);
            this.IsCompleted = true;
            this._finalContinuation?.Invoke();
        }

        internal void SetException(Exception exception)
        {
            if (exception is BreakException)
            {
                this._result = MaybeExResult.Nothing();
            }
            else
            {
                this._exception = exception;
            }
            this.IsCompleted = true;
            this._finalContinuation?.Invoke();
        }

        private MaybeExResult ValidateResult()
        {
            if (this._exception != null)
            {
                ExceptionDispatchInfo.Throw(this._exception);
            }

            if (!this.IsCompleted)
            {
                throw new Exception("Not Completed");
            }

            if (!this._result.HasValue)
            {
                throw new Exception("No result");
            }

            return this._result.Value;
        }

        public static implicit operator MaybeEx<T>(T value) => Value(value);

        public static MaybeEx<T> Nothing() => new MaybeEx<T>(MaybeExResult.Nothing());

        public static MaybeEx<T> Value(T value) => new MaybeEx<T>(MaybeExResult.Value(value));

        public struct MaybeExResult
        {
            public static MaybeExResult Value(T value) => new MaybeExResult(value, false);

            public static MaybeExResult Nothing() => new MaybeExResult(default, true);

            private MaybeExResult(T value, bool isNothing)
            {
                this._value = value;
                this.IsNothing = isNothing;
            }

            private T _value;

            public bool IsNothing { get; }

            public T GetValue()
            {
                if (this.IsNothing)
                {
                    throw new Exception("Nothing");
                }

                return this._value;
            }
        }

        public struct MaybeExResultAwaiter : INotifyCompletion
        {
            private readonly MaybeEx<T> _maybe;

            public MaybeExResultAwaiter(MaybeEx<T> maybe) => this._maybe = maybe;

            public bool IsCompleted => this._maybe.IsCompleted;

            public MaybeExResult GetResult() => this._maybe.ValidateResult();

            public MaybeExResultAwaiter GetAwaiter() => this;

            public void OnCompleted(Action continuation) => this._maybe.SetFinalContinuation(continuation);
        }

        private void SetFinalContinuation(Action finalContinuation)
        {
            this._finalContinuation = finalContinuation;
            if (this.IsCompleted)
            {
                finalContinuation();
            }
        }

        private Action _finalContinuation;
    }

    public class MaybeExTaskMethodBuilder<T>
    {
        public MaybeExTaskMethodBuilder() => this.Task = new MaybeEx<T>();

        public MaybeEx<T> Task { get; }

        private void GenericAwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
        {
            awaiter.OnCompleted(stateMachine.MoveNext);
        }

        public static MaybeExTaskMethodBuilder<T> Create() => new MaybeExTaskMethodBuilder<T>();

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void SetException(Exception exception) => this.Task.SetException(exception);

        public void SetResult(T result) => this.Task.SetResult(result);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
            => this.GenericAwaitOnCompleted(ref awaiter, ref stateMachine);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine) where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
            => this.GenericAwaitOnCompleted(ref awaiter, ref stateMachine);
    }
}