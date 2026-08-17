// // Services/ISmsService.cs
// using Twilio;
// using Twilio.Rest.Api.V2010.Account;

// public interface ISmsService
// {
//     Task<bool> SendSmsAsync(string phoneNumber, string message);
// }


// public class TwilioSmsService : ISmsService
// {
//     private readonly IConfiguration _configuration;
    
//     public TwilioSmsService(IConfiguration configuration)
//     {
//         _configuration = configuration;
//     }
    
//     public async Task<bool> SendSmsAsync(string phoneNumber, string message)
//     {
//         try
//         {
//             var accountSid = _configuration["Twilio:AccountSid"];
//             var authToken = _configuration["Twilio:AuthToken"];
//             var fromPhone = _configuration["Twilio:FromPhoneNumber"];
            
//             TwilioClient.Init(accountSid, authToken);
            
//             var result = await MessageResource.CreateAsync(
//                 body: message,
//                 from: new Twilio.Types.PhoneNumber(fromPhone),
//                 to: new Twilio.Types.PhoneNumber(phoneNumber)
//             );
            
//             return result.Status == MessageResource.StatusEnum.Sent;
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"SMS Error: {ex.Message}");
//             return false;
//         }
//     }
// }