CREATE TABLE [Setting]
(
	[Id]                   INT            IDENTITY (1, 1) NOT NULL,
    [DateOfCreate]         DATETIME2 (7)  NOT NULL DEFAULT GETUTCDATE(),
    [DateOfModify]         DATETIME2 (7)  NOT NULL DEFAULT GETUTCDATE(),
    [Name] NVARCHAR(200) NOT NULL UNIQUE, 
    [Value] NVARCHAR(MAX) NOT NULL, 
    CONSTRAINT [PK_Setting] PRIMARY KEY CLUSTERED ([Id] ASC),
)
