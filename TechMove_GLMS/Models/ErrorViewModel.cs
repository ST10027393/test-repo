// FILE: TechMove_GLMS/Models/ErrorViewModel.cs
namespace TechMove_GLMS.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    
}

//Additional firebase error model to capture error details from firebase auth responses
public class FirebaseErrorModel
{
    public Error error { get; set; }
}

public class Error
{
    public int code { get; set; }
    public string message { get; set; }
    public List<Error> errors { get; set; }
}
