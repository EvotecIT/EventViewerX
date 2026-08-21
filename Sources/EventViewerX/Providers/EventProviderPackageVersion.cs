namespace EventViewerX.Providers;

internal sealed class EventProviderPackageVersion :
    IComparable<EventProviderPackageVersion> {

    private EventProviderPackageVersion(
        string major,
        string minor,
        string patch,
        string prerelease) {

        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    internal string Major { get; }
    internal string Minor { get; }
    internal string Patch { get; }
    internal string Prerelease { get; }

    internal static EventProviderPackageVersion Parse(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new FormatException(
                "Provider package version cannot be empty.");
        }
        string[] buildParts = value.Split('+');
        if (buildParts.Length > 2 ||
            (buildParts.Length == 2 &&
             !ValidIdentifiers(
                 buildParts[1],
                 rejectNumericLeadingZero: false))) {
            throw new FormatException(
                $"Provider package version '{value}' contains invalid build metadata.");
        }
        string withoutBuild = buildParts[0];
        string[] releaseParts = withoutBuild.Split(
            new[] { '-' },
            2);
        string[] numbers = releaseParts[0].Split('.');
        if (numbers.Length != 3 ||
            !IsValidCoreNumber(numbers[0]) ||
            !IsValidCoreNumber(numbers[1]) ||
            !IsValidCoreNumber(numbers[2])) {
            throw new FormatException(
                $"Provider package version '{value}' must be SemVer in major.minor.patch form.");
        }
        string prerelease =
            releaseParts.Length == 2
                ? releaseParts[1]
                : string.Empty;
        if (releaseParts.Length == 2 &&
            !ValidIdentifiers(
                prerelease,
                rejectNumericLeadingZero: true)) {
            throw new FormatException(
                $"Provider package version '{value}' contains an invalid prerelease identifier.");
        }
        return new EventProviderPackageVersion(
            numbers[0],
            numbers[1],
            numbers[2],
            prerelease);
    }

    public int CompareTo(EventProviderPackageVersion? other) {
        if (other == null) {
            return 1;
        }
        int comparison = CompareNumericIdentifiers(
            Major,
            other.Major);
        if (comparison != 0) {
            return comparison;
        }
        comparison = CompareNumericIdentifiers(
            Minor,
            other.Minor);
        if (comparison != 0) {
            return comparison;
        }
        comparison = CompareNumericIdentifiers(
            Patch,
            other.Patch);
        if (comparison != 0) {
            return comparison;
        }
        if (Prerelease.Length == 0) {
            return other.Prerelease.Length == 0 ? 0 : 1;
        }
        if (other.Prerelease.Length == 0) {
            return -1;
        }

        string[] left = Prerelease.Split('.');
        string[] right = other.Prerelease.Split('.');
        int length = Math.Max(left.Length, right.Length);
        for (int index = 0; index < length; index++) {
            if (index >= left.Length) {
                return -1;
            }
            if (index >= right.Length) {
                return 1;
            }
            bool leftNumeric =
                IsNumericIdentifier(left[index]);
            bool rightNumeric =
                IsNumericIdentifier(right[index]);
            if (leftNumeric && rightNumeric) {
                comparison = CompareNumericIdentifiers(
                    left[index],
                    right[index]);
            } else if (leftNumeric) {
                comparison = -1;
            } else if (rightNumeric) {
                comparison = 1;
            } else {
                comparison = string.CompareOrdinal(
                    left[index],
                    right[index]);
            }
            if (comparison != 0) {
                return comparison;
            }
        }
        return 0;
    }

    private static int CompareNumericIdentifiers(
        string left,
        string right) {

        int lengthComparison =
            left.Length.CompareTo(right.Length);
        return lengthComparison != 0
            ? lengthComparison
            : string.CompareOrdinal(left, right);
    }

    private static bool IsValidCoreNumber(string value) {
        return IsNumericIdentifier(value) &&
               (value.Length == 1 || value[0] != '0');
    }

    private static bool IsNumericIdentifier(string value) {
        return value.Length > 0 &&
               value.All(static character =>
                   character >= '0' &&
                   character <= '9');
    }

    private static bool ValidIdentifiers(
        string value,
        bool rejectNumericLeadingZero) {

        if (value.Length == 0) {
            return false;
        }
        foreach (string identifier in value.Split('.')) {
            if (identifier.Length == 0 ||
                identifier.Any(static character =>
                    !IsAsciiLetterOrDigit(character) &&
                    character != '-')) {
                return false;
            }
            if (rejectNumericLeadingZero &&
                identifier.Length > 1 &&
                identifier[0] == '0' &&
                IsNumericIdentifier(identifier)) {
                return false;
            }
        }
        return true;
    }

    private static bool IsAsciiLetterOrDigit(char character) {
        return character >= '0' && character <= '9' ||
               character >= 'A' && character <= 'Z' ||
               character >= 'a' && character <= 'z';
    }
}
