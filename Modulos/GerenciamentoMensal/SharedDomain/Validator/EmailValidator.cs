using System.Text.RegularExpressions;

namespace SharedDomain.Validator;

public class EmailValidator
{
    private static readonly Regex EmailRegex = new Regex(
@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var quantidadeAroba = email.Count(c => c == '@');
        if (quantidadeAroba != 1) return false;

        return EmailRegex.IsMatch(email);
    }
}
