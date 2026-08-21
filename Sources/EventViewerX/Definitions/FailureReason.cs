namespace EventViewerX;

/// <summary>
/// Failure reasons returned for unsuccessful authentication attempts.
/// </summary>
public enum FailureReason {
    /// <summary>No failure reason provided.</summary>
    None = 0,
    // Common failure reasons (prefixed with %%)
    /// <summary>Unknown username or invalid password.</summary>
    UnknownUserNameOrBadPassword = 2304,
    /// <summary>Account restrictions prevented the logon.</summary>
    AccountRestrictions = 2305,
    /// <summary>Account is locked out.</summary>
    AccountLockedOut = 2307,
    /// <summary>Account is expired.</summary>
    AccountExpired = 2306,
    /// <summary>Requested logon type is not granted.</summary>
    LogonTypeNotGranted = 2309,
    /// <summary>Password is expired.</summary>
    PasswordExpired = 2308
}
