/* =============================================================================
   Universal Item Master — 08_schema_governance.sql
   Chapter 8 — approval workflow, audit trail, version control, obsolescence.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   §8.2.1 ApprovalWorkflowTemplate
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ApprovalWorkflowTemplate', N'U') IS NULL
CREATE TABLE dbo.ApprovalWorkflowTemplate
(
    WorkflowTemplateId   INT IDENTITY(1,1)  NOT NULL,
    TemplateCode         VARCHAR(30)        NOT NULL,
    TemplateName         VARCHAR(100)       NOT NULL,
    ItemCategoryId       INT                NULL,
    CreatedBy            INT                NOT NULL,
    CreatedDate          DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy           INT                NULL,
    ModifiedDate         DATETIME2(3)       NULL,
    IsActive             BIT                NOT NULL DEFAULT (1),
    IsDeleted            BIT                NOT NULL DEFAULT (0),
    RowVersion           ROWVERSION         NOT NULL,
    CONSTRAINT PK_ApprovalWorkflowTemplate PRIMARY KEY CLUSTERED (WorkflowTemplateId),
    CONSTRAINT FK_ApprovalWorkflowTemplate_Category FOREIGN KEY (ItemCategoryId)
        REFERENCES dbo.ItemCategory (ItemCategoryId),
    CONSTRAINT UQ_ApprovalWorkflowTemplate_Code UNIQUE (TemplateCode)
);
GO

/* One default workflow per category (a category cannot have two competing defaults). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ApprovalWorkflowTemplate_Category'
               AND object_id = OBJECT_ID(N'dbo.ApprovalWorkflowTemplate'))
    CREATE UNIQUE INDEX UX_ApprovalWorkflowTemplate_Category
        ON dbo.ApprovalWorkflowTemplate (ItemCategoryId)
        WHERE ItemCategoryId IS NOT NULL AND IsDeleted = 0;
GO

/* --------------------------------------------------------------------------
   §8.2.2 ApprovalStep
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ApprovalStep', N'U') IS NULL
CREATE TABLE dbo.ApprovalStep
(
    ApprovalStepId       INT IDENTITY(1,1)  NOT NULL,
    WorkflowTemplateId   INT                NOT NULL,
    StepOrder            INT                NOT NULL,
    ApproverRole         VARCHAR(50)        NOT NULL,
    IsMandatory          BIT                NOT NULL DEFAULT (1),
    CreatedBy            INT                NOT NULL,
    CreatedDate          DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy           INT                NULL,
    ModifiedDate         DATETIME2(3)       NULL,
    IsActive             BIT                NOT NULL DEFAULT (1),
    IsDeleted            BIT                NOT NULL DEFAULT (0),
    RowVersion           ROWVERSION         NOT NULL,
    CONSTRAINT PK_ApprovalStep PRIMARY KEY CLUSTERED (ApprovalStepId),
    CONSTRAINT FK_ApprovalStep_Template FOREIGN KEY (WorkflowTemplateId)
        REFERENCES dbo.ApprovalWorkflowTemplate (WorkflowTemplateId),
    CONSTRAINT UQ_ApprovalStep_TemplateOrder UNIQUE (WorkflowTemplateId, StepOrder),
    CONSTRAINT CK_ApprovalStep_Order CHECK (StepOrder >= 1)
);
GO

/* --------------------------------------------------------------------------
   §8.2.3 ItemApprovalRequest
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemApprovalRequest', N'U') IS NULL
CREATE TABLE dbo.ItemApprovalRequest
(
    RequestId            BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId               BIGINT                NOT NULL,
    WorkflowTemplateId   INT                   NOT NULL,
    RequestedBy          INT                   NOT NULL,
    RequestedDate        DATETIME2(3)          NOT NULL,
    CurrentStepOrder     INT                   NOT NULL DEFAULT (1),
    OverallStatus        VARCHAR(20)           NOT NULL DEFAULT ('Pending'),
    /* Carries the pending edit for a PATCH against an Active item (§10.6) so the
       change is not written to ItemMaster until the workflow completes. */
    PendingVersionNumber INT                   NULL,
    PendingChangeJson    NVARCHAR(MAX)         NULL,
    ChangeReason         VARCHAR(500)          NULL,
    CreatedBy            INT                   NOT NULL,
    CreatedDate          DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy           INT                   NULL,
    ModifiedDate         DATETIME2(3)          NULL,
    IsActive             BIT                   NOT NULL DEFAULT (1),
    IsDeleted            BIT                   NOT NULL DEFAULT (0),
    RowVersion           ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemApprovalRequest PRIMARY KEY CLUSTERED (RequestId),
    CONSTRAINT FK_ItemApprovalRequest_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemApprovalRequest_Template FOREIGN KEY (WorkflowTemplateId)
        REFERENCES dbo.ApprovalWorkflowTemplate (WorkflowTemplateId),
    CONSTRAINT FK_ItemApprovalRequest_RequestedBy FOREIGN KEY (RequestedBy) REFERENCES dbo.UserAccount (UserId),
    CONSTRAINT CK_ItemApprovalRequest_Status CHECK (OverallStatus IN ('Pending','Approved','Rejected')),
    CONSTRAINT CK_ItemApprovalRequest_StepOrder CHECK (CurrentStepOrder >= 1)
);
GO

/* At most one Pending request per item at a time. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ItemApprovalRequest_OnePendingPerItem'
               AND object_id = OBJECT_ID(N'dbo.ItemApprovalRequest'))
    CREATE UNIQUE INDEX UX_ItemApprovalRequest_OnePendingPerItem
        ON dbo.ItemApprovalRequest (ItemId)
        WHERE OverallStatus = 'Pending' AND IsDeleted = 0;
GO

/* --------------------------------------------------------------------------
   §8.2.4 ItemApprovalAction
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemApprovalAction', N'U') IS NULL
CREATE TABLE dbo.ItemApprovalAction
(
    ActionId       BIGINT IDENTITY(1,1)  NOT NULL,
    RequestId      BIGINT                NOT NULL,
    StepOrder      INT                   NOT NULL,
    ActionedBy     INT                   NOT NULL,
    ActionDate     DATETIME2(3)          NOT NULL,
    Decision       VARCHAR(20)           NOT NULL,
    Comments       VARCHAR(1000)         NULL,
    CreatedBy      INT                   NOT NULL,
    CreatedDate    DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy     INT                   NULL,
    ModifiedDate   DATETIME2(3)          NULL,
    IsActive       BIT                   NOT NULL DEFAULT (1),
    IsDeleted      BIT                   NOT NULL DEFAULT (0),
    RowVersion     ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemApprovalAction PRIMARY KEY CLUSTERED (ActionId),
    CONSTRAINT FK_ItemApprovalAction_Request FOREIGN KEY (RequestId)
        REFERENCES dbo.ItemApprovalRequest (RequestId),
    CONSTRAINT FK_ItemApprovalAction_ActionedBy FOREIGN KEY (ActionedBy) REFERENCES dbo.UserAccount (UserId),
    CONSTRAINT UQ_ItemApprovalAction_RequestStep UNIQUE (RequestId, StepOrder),
    CONSTRAINT CK_ItemApprovalAction_Decision CHECK (Decision IN ('Approved','Rejected'))
);
GO

/* --------------------------------------------------------------------------
   §8.3.1 ItemAuditLog — append-only, written exclusively by triggers.
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemAuditLog', N'U') IS NULL
CREATE TABLE dbo.ItemAuditLog
(
    AuditLogId     BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId         BIGINT                NOT NULL,
    TableName      VARCHAR(50)           NOT NULL,
    FieldName      VARCHAR(100)          NOT NULL,
    OldValue       NVARCHAR(1000)        NULL,
    NewValue       NVARCHAR(1000)        NULL,
    ChangeType     VARCHAR(20)           NOT NULL,
    ChangedBy      INT                   NOT NULL,
    ChangedDate    DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    CreatedBy      INT                   NOT NULL,
    CreatedDate    DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy     INT                   NULL,
    ModifiedDate   DATETIME2(3)          NULL,
    IsActive       BIT                   NOT NULL DEFAULT (1),
    IsDeleted      BIT                   NOT NULL DEFAULT (0),
    RowVersion     ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemAuditLog PRIMARY KEY CLUSTERED (AuditLogId),
    CONSTRAINT FK_ItemAuditLog_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT CK_ItemAuditLog_TableName CHECK (TableName IN ('ItemMaster','ItemAttribute')),
    CONSTRAINT CK_ItemAuditLog_ChangeType CHECK (ChangeType IN ('Insert','Update','SoftDelete'))
);
GO

/* --------------------------------------------------------------------------
   §8.4.1 ItemVersion
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemVersion', N'U') IS NULL
CREATE TABLE dbo.ItemVersion
(
    ItemVersionId       BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId              BIGINT                NOT NULL,
    VersionNumber       INT                   NOT NULL,
    SnapshotJson        NVARCHAR(MAX)         NOT NULL,
    ChangeReason        VARCHAR(500)          NULL,
    ApprovalRequestId   BIGINT                NULL,
    CreatedBy           INT                   NOT NULL,
    CreatedDate         DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy          INT                   NULL,
    ModifiedDate        DATETIME2(3)          NULL,
    IsActive            BIT                   NOT NULL DEFAULT (1),
    IsDeleted           BIT                   NOT NULL DEFAULT (0),
    RowVersion          ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemVersion PRIMARY KEY CLUSTERED (ItemVersionId),
    CONSTRAINT FK_ItemVersion_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemVersion_ApprovalRequest FOREIGN KEY (ApprovalRequestId)
        REFERENCES dbo.ItemApprovalRequest (RequestId),
    CONSTRAINT UQ_ItemVersion_ItemVersionNumber UNIQUE (ItemId, VersionNumber),
    CONSTRAINT CK_ItemVersion_Number CHECK (VersionNumber >= 1)
);
GO

/* --------------------------------------------------------------------------
   §8.5.1 ItemObsolescence
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemObsolescence', N'U') IS NULL
CREATE TABLE dbo.ItemObsolescence
(
    ObsolescenceId      INT IDENTITY(1,1)  NOT NULL,
    ItemId              BIGINT             NOT NULL,
    ReplacementItemId   BIGINT             NULL,
    ObsoleteDate        DATE               NOT NULL,
    Reason              VARCHAR(500)       NOT NULL,
    CreatedBy           INT                NOT NULL,
    CreatedDate         DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy          INT                NULL,
    ModifiedDate        DATETIME2(3)       NULL,
    IsActive            BIT                NOT NULL DEFAULT (1),
    IsDeleted           BIT                NOT NULL DEFAULT (0),
    RowVersion          ROWVERSION         NOT NULL,
    CONSTRAINT PK_ItemObsolescence PRIMARY KEY CLUSTERED (ObsolescenceId),
    CONSTRAINT FK_ItemObsolescence_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemObsolescence_Replacement FOREIGN KEY (ReplacementItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT CK_ItemObsolescence_NotSelf CHECK (ReplacementItemId IS NULL OR ReplacementItemId <> ItemId)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '08_schema_governance.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('08_schema_governance.sql');
GO
