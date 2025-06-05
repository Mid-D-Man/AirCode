
using System.ComponentModel.DataAnnotations;
using AirCode.Domain.Enums;
using AirCode.Domain.Interfaces;
using AirCode.Utilities.HelperScripts;

namespace AirCode.Domain.Entities
{
    //hmm for this should we leave it as record or sturct
    //should it be possible to edit academic session  or not
    //cause something might happen we dont know that
    //ok leave it as record but make it copyable,deletable just incase of incasities
    public record AcademicSession : ISecureEntity
    {
        [Required]
        public string SessionId { get; init; }
        public short YearStart { get; init; }
        public short YearEnd { get; init; }
        public List<Semester> Semesters { get; init; }
        
        // Security attributes from [ISecureEntity]
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }

        public override string ToString()
        {
            return MID_HelperFunctions.GetStructOrClassMemberValues(this);
        }

        internal string ToJson()
        {
            return JsonHelper.Serialize(this, true);
        }
    }

    public record Semester : ISecureEntity
    {
        [Required]
        public string SemesterId { get; init; }
        public SemesterType Type { get; init; }
        public string SessionId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        
        // Security attributes
        public string SecurityToken { get; init; }
        public DateTime LastModified { get; init; }
        public string ModifiedBy { get; init; }
        
        public override string ToString()
        {
            return MID_HelperFunctions.GetStructOrClassMemberValues(this);
        }
        
        internal string ToJson()
        {
            return JsonHelper.Serialize(this, true);
        }
    }
}