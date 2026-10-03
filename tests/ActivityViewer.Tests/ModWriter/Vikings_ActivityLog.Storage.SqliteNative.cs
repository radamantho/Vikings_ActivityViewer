using System;
using System.Runtime.InteropServices;

namespace Vikings_ActivityLog
{
    internal static class SqliteNative
    {
        internal const int Ok = 0;
        internal const int Row = 100;
        internal const int Done = 101;
        internal const int OpenReadWrite = 0x2;
        internal const int OpenCreate = 0x4;
        internal const int OpenFullMutex = 0x10000;
        internal static readonly IntPtr Transient = new IntPtr(-1);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int OpenV2Fn(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CloseV2Fn(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ExecFn(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, out IntPtr errorMessage);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void FreeFn(IntPtr pointer);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr ErrMsgFn(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int PrepareV2Fn(IntPtr db, byte[] sql, int bytes, out IntPtr statement, IntPtr tail);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindInt64Fn(IntPtr statement, int index, long value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindDoubleFn(IntPtr statement, int index, double value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindTextFn(IntPtr statement, int index, byte[] value, int bytes, IntPtr destructor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindNullFn(IntPtr statement, int index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int StepFn(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ResetFn(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ClearBindingsFn(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int FinalizeFn(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate long LastInsertRowIdFn(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ChangesFn(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate long ColumnInt64Fn(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr ColumnTextFn(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ColumnBytesFn(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BusyTimeoutFn(IntPtr db, int milliseconds);

        internal static OpenV2Fn OpenV2 = null!;
        internal static CloseV2Fn CloseV2 = null!;
        internal static ExecFn Exec = null!;
        internal static FreeFn Free = null!;
        internal static ErrMsgFn ErrMsg = null!;
        internal static PrepareV2Fn PrepareV2 = null!;
        internal static BindInt64Fn BindInt64 = null!;
        internal static BindDoubleFn BindDouble = null!;
        internal static BindTextFn BindText = null!;
        internal static BindNullFn BindNull = null!;
        internal static StepFn Step = null!;
        internal static ResetFn Reset = null!;
        internal static ClearBindingsFn ClearBindings = null!;
        internal static FinalizeFn FinalizeStatement = null!;
        internal static LastInsertRowIdFn LastInsertRowId = null!;
        internal static ChangesFn Changes = null!;
        internal static ColumnInt64Fn ColumnInt64 = null!;
        internal static ColumnTextFn ColumnText = null!;
        internal static ColumnBytesFn ColumnBytes = null!;
        internal static BusyTimeoutFn BusyTimeout = null!;

        private static readonly object LoadLock = new object();

        internal static bool IsLoaded { get; private set; }

        internal static void Load(string directory)
        {
            lock (LoadLock)
            {
                if (IsLoaded) return;

                IntPtr library = NativeLoader.Load(directory);
                OpenV2 = Get<OpenV2Fn>(library, "sqlite3_open_v2");
                CloseV2 = Get<CloseV2Fn>(library, "sqlite3_close_v2");
                Exec = Get<ExecFn>(library, "sqlite3_exec");
                Free = Get<FreeFn>(library, "sqlite3_free");
                ErrMsg = Get<ErrMsgFn>(library, "sqlite3_errmsg");
                PrepareV2 = Get<PrepareV2Fn>(library, "sqlite3_prepare_v2");
                BindInt64 = Get<BindInt64Fn>(library, "sqlite3_bind_int64");
                BindDouble = Get<BindDoubleFn>(library, "sqlite3_bind_double");
                BindText = Get<BindTextFn>(library, "sqlite3_bind_text");
                BindNull = Get<BindNullFn>(library, "sqlite3_bind_null");
                Step = Get<StepFn>(library, "sqlite3_step");
                Reset = Get<ResetFn>(library, "sqlite3_reset");
                ClearBindings = Get<ClearBindingsFn>(library, "sqlite3_clear_bindings");
                FinalizeStatement = Get<FinalizeFn>(library, "sqlite3_finalize");
                LastInsertRowId = Get<LastInsertRowIdFn>(library, "sqlite3_last_insert_rowid");
                Changes = Get<ChangesFn>(library, "sqlite3_changes");
                ColumnInt64 = Get<ColumnInt64Fn>(library, "sqlite3_column_int64");
                ColumnText = Get<ColumnTextFn>(library, "sqlite3_column_text");
                ColumnBytes = Get<ColumnBytesFn>(library, "sqlite3_column_bytes");
                BusyTimeout = Get<BusyTimeoutFn>(library, "sqlite3_busy_timeout");
                IsLoaded = true;
            }
        }

        private static T Get<T>(IntPtr library, string name) where T : Delegate
        {
            return (T)Marshal.GetDelegateForFunctionPointer(NativeLoader.Symbol(library, name), typeof(T));
        }
    }
}
