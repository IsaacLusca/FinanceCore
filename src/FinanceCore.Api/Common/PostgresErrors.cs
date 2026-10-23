using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceCore.Api.Common;

public static class PostgresErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgresException &&
        postgresException.ConstraintName == constraintName;

    public static bool IsConcurrencyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
        };

    public static bool IsCheckViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.CheckViolation
        };
}
