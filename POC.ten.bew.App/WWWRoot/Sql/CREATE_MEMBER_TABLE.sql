﻿/***** OK SO DON'T LEAVE THIS FILE IN YOUR WEBSERVER - IT'S NOT REQUIRED AT RUNTIME - JUST HERE FOR YOUR USE TO SET UP THIS SAMPLE *****/

USE [Test]
GO

/****** Object:  Table [dbo].[Member]    Script Date: 09/06/2013 10:54:48 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

SET ANSI_PADDING ON
GO

CREATE TABLE [dbo].[Member](
	[MemberID] [int] NOT NULL,
	[Name] [varchar](16) NOT NULL,
 CONSTRAINT [PK_Member] PRIMARY KEY CLUSTERED 
(
	[MemberID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

SET ANSI_PADDING OFF
GO

