-- Local development demo data. This is a placeholder contact, never a real recipient.
-- Run against the ServiceDesk development database after applying its migrations.
SET NOCOUNT ON;

IF NOT EXISTS (
    SELECT 1 FROM dbo.ExternalTechnicians
    WHERE FullName = N'Demo Technician' AND CompanyName = N'ServiceDesk Demo'
)
BEGIN
    INSERT INTO dbo.ExternalTechnicians (FullName, CompanyName, PhoneNumber, Specialisation, IsActive)
    VALUES (N'Demo Technician', N'ServiceDesk Demo', N'00000000', N'General maintenance (demo)', 1);
END;

SELECT Id, FullName, CompanyName FROM dbo.ExternalTechnicians
WHERE FullName = N'Demo Technician' AND CompanyName = N'ServiceDesk Demo';
