using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace System.Threading.Tasks.Sources
{
    [StructLayout(LayoutKind.Auto)]
    public struct ManualResetValueTaskSourceCore<TResult>
    {

        private Action<object> _continuation;
        private object _continuationState;
        private ExecutionContext _executionContext;
        private object _capturedContext;
        private bool _completed;
        private TResult _result;
        private ExceptionDispatchInfo _error;
        private short _version;

        public bool RunContinuationsAsynchronously { get; set; }

        public void Reset()
        {
            _version++;
            _completed = false;
            _result = default;
            _error = null;
            _executionContext = null;
            _capturedContext = null;
            _continuation = null;
            _continuationState = null;
        }

        public void SetResult(TResult result)
        {
            _result = result;
            SignalCompletion();
        }

        public void SetException(Exception error)
        {
            _error = ExceptionDispatchInfo.Capture(error);
            SignalCompletion();
        }

        public short Version => _version;

        public ValueTaskSourceStatus GetStatus(short token)
        {
            ValidateToken(token);
            return
                !_completed ? ValueTaskSourceStatus.Pending :
                _error == null ? ValueTaskSourceStatus.Succeeded :
                _error.SourceException is OperationCanceledException ? ValueTaskSourceStatus.Canceled :
                ValueTaskSourceStatus.Faulted;
        }

        [StackTraceHidden]
        public TResult GetResult(short token)
        {
            ValidateToken(token);
            if (!_completed)
            {
                ManualResetValueTaskSourceCoreShared.ThrowInvalidOperationException();
            }

            _error?.Throw();
            return _result;
        }
               
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }
            ValidateToken(token);

            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                SynchronizationContext sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    _capturedContext = sc;
                }
                else
                {
                    TaskScheduler ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        _capturedContext = ts;
                    }
                }
            }

            object oldContinuation = _continuation;
            if (oldContinuation == null)
            {
                _continuationState = state;
                oldContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);
            }

            if (oldContinuation != null)
            {
                if (!ReferenceEquals(oldContinuation, ManualResetValueTaskSourceCoreShared.s_sentinel))
                {
                    ManualResetValueTaskSourceCoreShared.ThrowInvalidOperationException();
                }

                switch (_capturedContext)
                {
                    case null:
                        if (_executionContext != null)
                        {
                            // REVIEW: Original call was
                            // ThreadPool.QueueUserWorkItem(continuation, state, preferLocal: true);
                            ThreadPool.QueueUserWorkItem(s =>continuation(s), state); 
                        }
                        else
                        {
                            // REVIEW: Original call was
                            // ThreadPool.UnsafeQueueUserWorkItem(continuation, state, preferLocal: true);
                            ThreadPool.UnsafeQueueUserWorkItem(s => continuation(s), state); 
                        }
                        break;

                    case SynchronizationContext sc:
                        sc.Post(s =>
                        {
                            var tuple = (Tuple<Action<object>, object>)s;
                            tuple.Item1(tuple.Item2);
                        }, Tuple.Create(continuation, state));
                        break;

                    case TaskScheduler ts:
                        Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                        break;
                }
            }
        }

        private void ValidateToken(short token)
        {
            if (token != _version)
            {
                ManualResetValueTaskSourceCoreShared.ThrowInvalidOperationException();
            }
        }

        private void SignalCompletion()
        {
            if (_completed)
            {
                ManualResetValueTaskSourceCoreShared.ThrowInvalidOperationException();
            }
            _completed = true;

            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, ManualResetValueTaskSourceCoreShared.s_sentinel, null) != null)
            {
                if (_executionContext != null)
                {
                    // REVIEW: Original call was
                    // ExecutionContext.RunInternal(
                    //    _executionContext,
                    //    (ref ManualResetValueTaskSourceCore<TResult> s) => s.InvokeContinuation(),
                    //    ref this);

                    ExecutionContext.Run(
                        _executionContext,
                        (s => ((ManualResetValueTaskSourceCore<TResult>)s).InvokeContinuation()), 
                        this);
                }
                else
                {
                    InvokeContinuation();
                }
            }
        }

        private void InvokeContinuation()
        {
            switch (_capturedContext)
            {
                case null:
                    if (RunContinuationsAsynchronously)
                    {
                        if (_executionContext != null)
                        {
                            // REVIEW: Assigned to local to avoid "Anonymous methods, lambda expressions, and query expressions inside structs cannot access instance members of 'this'."
                            var continuation = _continuation;
                            // REVIEW: Original call was
                            // ThreadPool.QueueUserWorkItem(_continuation, _continuationState, preferLocal: true);
                            ThreadPool.QueueUserWorkItem(s => continuation(s), _continuationState); // , preferLocal: true
                        }
                        else
                        {
                            // REVIEW: Assigned to local to avoid "Anonymous methods, lambda expressions, and query expressions inside structs cannot access instance members of 'this'."
                            var continuation = _continuation;
                            // REVIEW: Original call was
                            // ThreadPool.UnsafeQueueUserWorkItem(_continuation, _continuationState, preferLocal: true);
                            ThreadPool.UnsafeQueueUserWorkItem(s => continuation(s), _continuationState); // , preferLocal: true
                        }
                    }
                    else
                    {
                        _continuation(_continuationState);
                    }
                    break;

                case SynchronizationContext sc:
                    sc.Post(s =>
                    {
                        var state = (Tuple<Action<object>, object>)s;
                        state.Item1(state.Item2);
                    }, Tuple.Create(_continuation, _continuationState));
                    break;

                case TaskScheduler ts:
                    Task.Factory.StartNew(_continuation, _continuationState, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                    break;
            }
        }
    }

    internal static class ManualResetValueTaskSourceCoreShared
    {
        [StackTraceHidden]
        internal static void ThrowInvalidOperationException() => throw new InvalidOperationException();

        internal static readonly Action<object> s_sentinel = CompletionSentinel;
        private static void CompletionSentinel(object _)
        {
            Debug.Fail("The sentinel delegate should never be invoked.");
            ThrowInvalidOperationException();
        }
    }
}