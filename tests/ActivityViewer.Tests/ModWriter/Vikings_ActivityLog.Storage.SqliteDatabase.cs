using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Vikings_ActivityLog
{
    internal sealed class SqliteException : Exception
    {
        internal SqliteException(string message) : base(message) { }
    }

    internal sealed class SqliteDatabase : IDisposable
    {
        private IntPtr _handle;

        internal SqliteDatabase(string path)
        {
            int flags = SqliteNative.OpenReadWrite | SqliteNative.OpenCreate | SqliteNative.OpenFullMutex;
            int result = SqliteNative.OpenV2(ToUtf8Z(path), out _handle, flags, IntPtr.Zero);
            if (result != SqliteNative.Ok)
            {
                string message = ErrorMessage();
                SqliteNative.CloseV2(_handle);
                _handle = IntPtr.Zero;
                throw new SqliteException("Could not open " + path + ": " + message);
            }

            SqliteNative.BusyTimeout(_handle, 5000);
        }

        internal IntPtr Handle => _handle;

        internal long LastInsertRowId => SqliteNative.LastInsertRowId(_handle);

        internal int Changes => SqliteNative.Changes(_handle);

        internal void Execute(string sql)
        {
            int result = SqliteNative.Exec(_handle, ToUtf8Z(sql), IntPtr.Zero, IntPtr.Zero, out IntPtr error);
            if (result == SqliteNative.Ok) return;

            string message = error != IntPtr.Zero ? FromUtf8Z(error) : ErrorMessage();
            if (error != IntPtr.Zero) SqliteNative.Free(error);
            throw new SqliteException(message);
        }

        internal SqliteStatement Prepare(string sql)
        {
            byte[] bytes = ToUtf8Z(sql);
            int result = SqliteNative.PrepareV2(_handle, bytes, bytes.Length, out IntPtr statement, IntPtr.Zero);
            if (result != SqliteNative.Ok) throw new SqliteException(ErrorMessage());
            return new SqliteStatement(this, statement);
        }

        internal long ScalarInt64(string sql)
        {
            using SqliteStatement statement = Prepare(sql);
            return statement.Step() ? statement.ColumnInt64(0) : 0;
        }

        internal string ScalarText(string sql)
        {
            using SqliteStatement statement = Prepare(sql);
            return statement.Step() ? statement.ColumnText(0) : "";
        }

        internal string ErrorMessage() => FromUtf8Z(SqliteNative.ErrMsg(_handle));

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            SqliteNative.CloseV2(_handle);
            _handle = IntPtr.Zero;
        }

        internal static byte[] ToUtf8Z(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] result = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        internal static string FromUtf8Z(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return "";
            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0) length++;
            byte[] buffer = new byte[length];
            Marshal.Copy(pointer, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }
    }

    internal sealed class SqliteStatement : IDisposable
    {
        private static readonly byte[] EmptyText = { 0 };

        private readonly SqliteDatabase _database;
        private IntPtr _handle;

        internal SqliteStatement(SqliteDatabase database, IntPtr handle)
        {
            _database = database;
            _handle = handle;
        }

        internal void Bind(int index, long value) => Check(SqliteNative.BindInt64(_handle, index, value));

        internal void Bind(int index, double value) => Check(SqliteNative.BindDouble(_handle, index, value));

        internal void Bind(int index, string? value)
        {
            if (value == null)
            {
                Check(SqliteNative.BindNull(_handle, index));
                return;
            }

            byte[] bytes = value.Length == 0 ? EmptyText : Encoding.UTF8.GetBytes(value);
            int length = value.Length == 0 ? 0 : bytes.Length;
            Check(SqliteNative.BindText(_handle, index, bytes, length, SqliteNative.Transient));
        }

        internal bool Step()
        {
            int result = SqliteNative.Step(_handle);
            if (result == SqliteNative.Row) return true;
            if (result == SqliteNative.Done) return false;
            throw new SqliteException(_database.ErrorMessage());
        }

        internal void Reset()
        {
            SqliteNative.Reset(_handle);
            SqliteNative.ClearBindings(_handle);
        }

        internal long ColumnInt64(int column) => SqliteNative.ColumnInt64(_handle, column);

        internal string ColumnText(int column)
        {
            IntPtr text = SqliteNative.ColumnText(_handle, column);
            int bytes = SqliteNative.ColumnBytes(_handle, column);
            if (text == IntPtr.Zero || bytes == 0) return "";
            byte[] buffer = new byte[bytes];
            Marshal.Copy(text, buffer, 0, bytes);
            return Encoding.UTF8.GetString(buffer);
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            SqliteNative.FinalizeStatement(_handle);
            _handle = IntPtr.Zero;
        }

        private void Check(int result)
        {
            if (result != SqliteNative.Ok) throw new SqliteException(_database.ErrorMessage());
        }
    }
}
