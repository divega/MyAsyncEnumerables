using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    [StructLayout(LayoutKind.Auto)]
    public struct AsyncIteratorMethodBuilder
    {
        private AsyncTaskMethodBuilder _methodBuilder;

        public static AsyncIteratorMethodBuilder Create() => default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveNext<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine =>
            _methodBuilder.Start(ref stateMachine);

        // REVIEW: Original code is 
        // #if CORERT
        //        _methodBuilder.Start(ref stateMachine);
        // #else
        //        AsyncMethodBuilderCore.Start(ref stateMachine);
        // #endif

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine =>
            _methodBuilder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine =>
            _methodBuilder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);

        public void Complete() => _methodBuilder.SetResult();

        // REVIEW: Removed internal
        // internal object ObjectIdForDebugger => _methodBuilder.ObjectIdForDebugger;
    }
}
