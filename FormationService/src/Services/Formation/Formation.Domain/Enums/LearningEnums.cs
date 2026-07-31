namespace Formation.Domain.Enums;

public enum TrainingResourceType
{
    Pdf = 0,
    Video = 1,
    Link = 2,
    Text = 3,
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
