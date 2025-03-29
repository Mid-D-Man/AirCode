namespace AirCode.Domain.Enums;

public enum AttendanceType
{
    Present,
    Late,
    Absent
}

public enum SessionMode
{
    Online,
    Offline,
    Hybrid
}

public enum AttendanceVerificationMethod
{
    QRCode,
    Manual
}