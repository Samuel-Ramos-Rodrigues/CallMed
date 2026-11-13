using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
internal static class PgTestDdl
{
    // Only identifier names from the test's constant lists are accepted. SQL
    // identifiers cannot be bound as parameters; no external input is used.
    internal static Task ExecuteAsync(MKSANContext db, string prefix, string identifier, string suffix)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[A-Za-z][A-Za-z0-9_]*$")) throw new ArgumentException("Invalid fixture identifier.");
        var sql = prefix + identifier + suffix;
        return db.Database.ExecuteSqlRawAsync(sql);
    }
}
