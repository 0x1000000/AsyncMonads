using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AsyncMonads
{
    [AsyncMethodBuilder(typeof(MaybeTaskMethodBuilder<>))]
    public class Maybe<T> : IMaybe, INotifyCompletion
    {
        private MaybeResult? _result;

        private Action _continuation;

        private Exception _exception;

        private IMaybe _parent;

        private bool _forExit;

        internal Maybe() { }//Used in async method

        private Maybe(MaybeResult result) => this._result = result;// "Resolved" instance 

        public bool IsCompleted { get; private set; }

        public void OnCompleted(Action continuation)
        {
            if (this._continuation != null)
            {
                throw new Exception("Only one continuation is allowed");
            }

            this._continuation = continuation;
            if (this._forExit)
            {
                ((IMaybe)this).Exit();
            }
            else if (this._result.HasValue)
            {
                this.NotifyResult(this._result.Value.IsNothing);
            }
        }

        void IMaybe.SetParent(IMaybe parent) => this._parent = parent;

        void IMaybe.Exit()
        {
            if (this._continuation == null)
            {
                this._forExit = true; //Sync
                return;
            }

            this._result = MaybeResult.Nothing();
            this.IsCompleted = true;

            if (this._parent != null)
            {
                this._parent.Exit();
            }
            else
            {
                this._continuation();
            }
        }

        public Maybe<T> GetAwaiter() => this;

        public MaybeResultAwaiter GetMaybeResult() => new MaybeResultAwaiter(this);

        public T GetResult() => this.ValidateResult().GetValue();

        internal void SetResult(T result)
        {
            this._result = MaybeResult.Value(result);
            this.IsCompleted = true;
            this.NotifyResult(this._result.Value.IsNothing);
        }

        internal void SetException(Exception exception)
        {
            this._exception = exception;
            this.IsCompleted = true;
            this._continuation?.Invoke();
        }

        

        private void NotifyResult(bool isNothing)
        {
            this.IsCompleted = true;
            if (isNothing)
            {
                this._parent.Exit();
            }
            else
            {
                this._continuation?.Invoke();
            }
        }

        private MaybeResult ValidateResult()
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

        public static implicit operator Maybe<T>(T value) => Value(value);

        public static Maybe<T> Nothing() => new Maybe<T>(MaybeResult.Nothing());

        public static Maybe<T> Value(T value) => new Maybe<T>(MaybeResult.Value(value));

        public struct MaybeResult
        {
            public static MaybeResult Value(T value) => new MaybeResult(value, false);

            public static MaybeResult Nothing() => new MaybeResult(default, true);

            private MaybeResult(T value, bool isNothing)
            {
                this._value = value;
                this.IsNothing = isNothing;
            }

            private readonly T _value;

            public readonly bool IsNothing;

            public T GetValue() => this.IsNothing ? throw new Exception("Nothing") : this._value;
        }

        public struct MaybeResultAwaiter : INotifyCompletion
        {
            private readonly Maybe<T> _maybe;

            public MaybeResultAwaiter(Maybe<T> maybe) => this._maybe = maybe;

            public bool IsCompleted => this._maybe.IsCompleted;

            public MaybeResult GetResult() => this._maybe.ValidateResult();

            public MaybeResultAwaiter GetAwaiter() => this;

            public void OnCompleted(Action continuation) => this._maybe.OnCompleted(continuation);
        }
    }

    public interface IMaybe
    {
        void SetParent(IMaybe exit);

        void Exit();
    }

    public class MaybeTaskMethodBuilder<T>
    {
        public MaybeTaskMethodBuilder() => this.Task = new Maybe<T>();

        public Maybe<T> Task { get; }

        private void GenericAwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine) 
            where TAwaiter : INotifyCompletion 
            where TStateMachine : IAsyncStateMachine
        {
            if (awaiter is IMaybe maybe)
            {
                maybe.SetParent(this.Task);
            }

            awaiter.OnCompleted(stateMachine.MoveNext);
        }

        public static MaybeTaskMethodBuilder<T> Create() => new MaybeTaskMethodBuilder<T>();

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