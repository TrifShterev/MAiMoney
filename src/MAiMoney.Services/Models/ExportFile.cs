namespace MAiMoney.Services.Models;

public record ExportFile(byte[] Content, string ContentType, string FileName);
