using System;
using System.Collections.Generic;

namespace VehicleCapUnlock.Core
{
    internal interface IReversibleWrite
    {
        bool IsCurrent();
        void Apply();
        bool TryRollback();
    }

    // Compare before writing, including during rollback: never overwrite a third value.
    internal sealed class ValueWrite<T> : IReversibleWrite
    {
        private readonly Func<T> m_Read;
        private readonly Action<T> m_Write;
        private readonly Func<T, T, bool> m_Equals;
        private readonly T m_Before;
        private readonly T m_After;

        public ValueWrite(Func<T> read, Action<T> write, T before, T after,
            Func<T, T, bool> equals = null)
        {
            m_Read = read;
            m_Write = write;
            m_Before = before;
            m_After = after;
            m_Equals = equals ?? EqualityComparer<T>.Default.Equals;
        }

        public bool IsCurrent() => m_Equals(m_Read(), m_Before);

        public void Apply()
        {
            if (!IsCurrent()) throw new InvalidOperationException("Data changed after preflight");
            m_Write(m_After);
        }

        public bool TryRollback()
        {
            T current = m_Read();
            if (m_Equals(current, m_Before)) return true;
            if (!m_Equals(current, m_After)) return false;
            m_Write(m_Before);
            return m_Equals(m_Read(), m_Before);
        }
    }

    internal sealed class WriteBatchException : Exception
    {
        public bool RollbackSucceeded { get; }
        public WriteBatchException(Exception error, bool rollbackSucceeded)
            : base("Write batch failed; rollback complete=" + rollbackSucceeded, error)
        {
            RollbackSucceeded = rollbackSucceeded;
        }
    }

    internal static class SafeWriteBatch
    {
        public static void Execute(IReadOnlyList<IReversibleWrite> writes)
        {
            // Preflight every change before the first mutation.
            foreach (var write in writes)
                if (!write.IsCurrent()) throw new InvalidOperationException("Preflight conflict; no writes applied");

            int attempted = -1;
            try
            {
                for (int i = 0; i < writes.Count; i++)
                {
                    attempted = i; // Include a setter that throws after performing its write.
                    writes[i].Apply();
                }
            }
            catch (Exception error)
            {
                bool complete = true;
                for (int i = attempted; i >= 0; i--)
                {
                    try { complete &= writes[i].TryRollback(); }
                    catch { complete = false; }
                }
                throw new WriteBatchException(error, complete);
            }
        }
    }

    internal sealed class OwnedValue<T>
    {
        public T Original { get; }
        public T LastApplied { get; private set; }
        public bool HasChanges => !EqualityComparer<T>.Default.Equals(Original, LastApplied);

        public OwnedValue(T original) { Original = LastApplied = original; }

        public bool Accepts(T current) => EqualityComparer<T>.Default.Equals(current, LastApplied) ||
            EqualityComparer<T>.Default.Equals(current, Original);

        public void Commit(T value) { LastApplied = value; }
    }

    internal sealed class PendingWork
    {
        private const int MaxAttempts = 3;
        public bool Pending { get; private set; } = true;
        public bool Failed { get; private set; }
        public int Attempts { get; private set; }
        public void Request() { Pending = true; Failed = false; Attempts = 0; }
        public void Complete() { Pending = false; Failed = false; Attempts = 0; }
        public void Fail(bool canRetry)
        {
            Attempts++;
            Pending = canRetry && Attempts < MaxAttempts;
            Failed = !Pending;
        }
    }
}
