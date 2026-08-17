

using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolManagement.Api.Models;

[Table("FcmTokens")]
public class FcmToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? fcmToken{ get; set; }
}