namespace ArmsFair.Server.Data.Entities;

public class PlayerEntity
{
    public Guid      Id                 { get; set; } = Guid.NewGuid();
    public string    Username           { get; set; } = null!;
    public string?   Email              { get; set; }
    public string?   PasswordHash       { get; set; }
    public string?   SteamId            { get; set; }
    public string    HomeNationIso      { get; set; } = "USA";
    public DateTime  CreatedAt          { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt        { get; set; }
    public DateTime? UsernameChangedAt  { get; set; }
    public bool      IsBanned           { get; set; }
}
