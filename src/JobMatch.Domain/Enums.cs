namespace JobMatch.Domain;

public enum UserRole
{
    Candidate,
    Employer
}

public enum CompanyMemberRole
{
    Owner
}

public enum WorkFormat
{
    Office,
    Remote,
    Hybrid
}

public enum VacancyStatus
{
    Draft,
    Published,
    Closed
}

public enum ApplicationStatus
{
    Submitted,
    Viewed,
    Invited,
    Rejected,
    Withdrawn
}

public enum CreditTransactionType
{
    DailyReset,
    Debit
}
