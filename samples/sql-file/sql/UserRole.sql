CREATE TABLE [UserRole]
(
    [Id]                       INT IDENTITY (1, 1) NOT NULL,
    [DateOfCreate]             DATETIME2 (7)  NOT NULL DEFAULT GETUTCDATE(),
    [DateOfModify]             DATETIME2 (7)  NOT NULL DEFAULT GETUTCDATE(),
	[UserId]                   INT            NOT NULL,
    [RoleId]                   INT            NOT NULL, 
    CONSTRAINT [FK_UserRole_To_User] FOREIGN KEY ([UserId]) REFERENCES [User]([Id]) ON DELETE CASCADE, 
    CONSTRAINT [FK_UserRole_To_Role] FOREIGN KEY ([RoleId]) REFERENCES [Role]([Id]) ON DELETE CASCADE, 
    CONSTRAINT [PK_UserRole] PRIMARY KEY ([Id]),
)
