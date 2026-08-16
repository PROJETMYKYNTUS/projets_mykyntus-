namespace Formation.Domain.Enums;

public enum TrainingResourceType
{
    Pdf = 0,
    Video = 1,
    Link = 2,
    Text = 3,
    Image = 4,
}

public enum CatalogAudienceMatchMode
{
    MatchAny = 0,
    MatchAll = 1,
}

public enum LearningGateMode
{
    Attendance = 0,
    Content = 1,
    Both = 2,
}

public enum CatalogItemStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

public enum CatalogDueMode
{
    None = 0,
    Absolute = 1,
    RelativeDays = 2,
}

public enum CatalogEnrollmentSource
{
    SelfService = 0,
    Session = 1,
}

public enum CatalogEnrollmentStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Overdue = 3,
}
