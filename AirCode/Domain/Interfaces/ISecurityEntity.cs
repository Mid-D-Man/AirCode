namespace AirCode.Domain.Interfaces;

// Interface for adding basic security attributes
public interface ISecureEntity
{
    string SecurityToken { get;  }
    DateTime LastModified { get;  }
    string ModifiedBy { get; }

   
}

public interface IModifiableSecurityEntity : ISecureEntity
{
    /// <summary>
    /// manually set sec details
    /// </summary>
    /// <param name="securityToken"></param>
    /// <param name="lastModified"></param>
    /// <param name="modifiedBy"></param>
    internal void SetModificationDetails(string securityToken, DateTime lastModified, string modifiedBy = "");
}