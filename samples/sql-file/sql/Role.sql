CREATE TABLE [Role]
(
	[Id]                   INT            IDENTITY (1, 1) NOT NULL,
    [DateOfCreate]         DATETIME2 (7)  NOT NULL DEFAULT GETUTCDATE(),
    [DateOfModify]         DATETIME2 (7)  NOT NULL DEFAULT GETUTCDATE(),
    [Name]                 NVARCHAR (200) NOT NULL,
    
    CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED ([Id] ASC), 
)
