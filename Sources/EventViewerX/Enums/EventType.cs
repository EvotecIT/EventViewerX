namespace EventViewerX {
    /// <summary>
    /// Defines the built-in event types that can be queried.
    /// </summary>
    public enum EventType {
        /// <summary>
        /// Active Directory computer account created or modified
        /// </summary>
        ADComputerCreateChange,
      
        /// <summary>
        /// Active Directory computer account deleted
        /// </summary>
        ADComputerDeleted,
      
        /// <summary>
        /// Detailed changes for computer accounts
        /// </summary>
        ADComputerChangeDetailed,

        /// <summary>
        /// Modifications to group membership
        /// </summary>
        ADGroupMembershipChange,
      
        /// <summary>
        /// Group membership enumeration events
        /// </summary>
        ADGroupEnumeration,
      
        /// <summary>
        /// Active Directory group created, changed or deleted
        /// </summary>
        ADGroupChange,
      
        /// <summary>
        /// Group creation or deletion events
        /// </summary>
        ADGroupCreateDelete,
      
        /// <summary>
        /// Detailed changes for group objects
        /// </summary>
        ADGroupChangeDetailed,

        /// <summary>
        /// Changes to Group Policy Objects
        /// </summary>
        ADGroupPolicyChanges,
      
        /// <summary>
        /// Edits to Group Policy Objects
        /// </summary>
        ADGroupPolicyEdits,
      
        /// <summary>
        /// Links or unlinks of Group Policy Objects
        /// </summary>
        ADGroupPolicyLinks,

        /// <summary>
        /// Detailed audit information for group policy changes
        /// </summary>
        ADGroupPolicyChangesDetailed,

        /// <summary>
        /// Group Policy Object created
        /// </summary>
        GpoCreated,
      
        /// <summary>
        /// Group Policy Object deleted
        /// </summary>
        GpoDeleted,
      
        /// <summary>
        /// Group Policy Object modified
        /// </summary>
        GpoModified,

        /// <summary>
        /// Summary of LDAP binding activity
        /// </summary>
        ADLdapBindingSummary,
      
        /// <summary>
        /// Detailed LDAP binding information
        /// </summary>
        ADLdapBindingDetails,
      
        /// <summary>
        /// Active Directory user account created or changed
        /// </summary>
        ADUserCreateChange,
      
        /// <summary>
        /// User account enabled, disabled, unlocked or deleted
        /// </summary>
        ADUserStatus,
      
        /// <summary>
        /// Detailed changes for user accounts
        /// </summary>
        ADUserChangeDetailed,
      
        /// <summary>
        /// User account lockout events
        /// </summary>
        ADUserLockouts,
      
        /// <summary>
        /// Successful user logon
        /// </summary>
        ADUserLogon,
      
        /// <summary>
        /// NTLMv1 logon tracking
        /// </summary>
        ADUserLogonNTLMv1,
      
        /// <summary>
        /// Failed user logon attempts
        /// </summary>
        ADUserLogonFailed,
      
        /// <summary>
        /// User account unlocked
        /// </summary>
        ADUserUnlocked,
      
        /// <summary>
        /// Special privileges assigned to new logon
        /// </summary>
        ADUserPrivilegeUse,
      
        /// <summary>
        /// User rights assigned or removed
        /// </summary>
        ADUserRightsAssignment,
      
        /// <summary>
        /// Kerberos TGT requests
        /// </summary>
        KerberosTGTRequest,

        /// <summary>
        /// Kerberos service ticket requests and renewals
        /// </summary>
        KerberosServiceTicket,
      
        /// <summary>
        /// Kerberos ticket request failures
        /// </summary>
        KerberosTicketFailure,
      
        /// <summary>
        /// Kerberos policy changed
        /// </summary>
        KerberosPolicyChange,

        /// <summary>
        /// Organizational unit created, deleted or moved
        /// </summary>
        ADOrganizationalUnitChangeDetailed,
      
        /// <summary>
        /// Detailed changes for other directory objects
        /// </summary>
        ADOtherChangeDetailed,

        /// <summary>
        /// SMB1 access audit information
        /// </summary>
        ADSMBServerAuditV1,

        /// <summary>
        /// Security log cleared
        /// </summary>
        LogsClearedSecurity,
      
        /// <summary>
        /// Application or system log cleared
        /// </summary>
        LogsClearedOther,
      
        /// <summary>
        /// Security log is full
        /// </summary>
        LogsFullSecurity,

        /// <summary>
        /// NPS granted or denied network access
        /// </summary>
        NetworkAccessAuthenticationPolicy,

        /// <summary>
        /// Certificate issued by Certificate Authority
        /// </summary>
        CertificateIssued,

        /// <summary>
        /// System audit policy was changed
        /// </summary>
        AuditPolicyChange,

        /// <summary>
        /// Windows Firewall rule modified
        /// </summary>
        FirewallRuleChange,

        /// <summary>
        /// DHCP lease creation event
        /// </summary>
        DhcpLeaseCreated,

        /// <summary>
        /// BitLocker protection key changed or backed up
        /// </summary>
        BitLockerKeyChange,

        /// <summary>
        /// BitLocker protection was suspended
        /// </summary>
        BitLockerSuspended,

        /// <summary>
        /// External device recognized by the system
        /// </summary>
        DeviceRecognized,

        /// <summary>
        /// Device was disabled
        /// </summary>
        DeviceDisabled,

        /// <summary>
        /// Object deleted
        /// </summary>
        ObjectDeletion,

        /// <summary>
        /// Scheduled task deleted
        /// </summary>
        ScheduledTaskDeleted,
        /// <summary>
        /// Scheduled task created
        /// </summary>
        ScheduledTaskCreated,

        /// <summary>
        /// Unexpected system shutdown
        /// </summary>
        OSCrash,

        /// <summary>
        /// Bugcheck event describing a system crash
        /// </summary>
        OSBugCheck,

        /// <summary>
        /// Operating system startup (System log)
        /// </summary>
        OSStartup,

        /// <summary>
        /// Operating system shutdown
        /// </summary>
        OSShutdown,

        /// <summary>
        /// Operating system rebooted without clean shutdown
        /// </summary>
        OSUncleanShutdown,

        /// <summary>
        /// Operating system startup (Security log)
        /// </summary>
        OSStartupSecurity,

        /// <summary>
        /// Administrator recovered system from CrashOnAuditFail
        /// </summary>
        OSCrashOnAuditFailRecovery,
      
        /// <summary>
        /// System time changed
        /// </summary>
        OSTimeChange,
      
        /// <summary>
        /// Windows Update installation failure
        /// </summary>
        WindowsUpdateFailure,
      
        /// <summary>
        /// Group Policy client-side processing events from Application log
        /// </summary>
        ClientGroupPoliciesApplication,
      
        /// <summary>
        /// Group Policy client-side processing events from System log
        /// </summary>
        ClientGroupPoliciesSystem,
      
        /// <summary>
        /// Hyper-V virtual machine was shut down
        /// </summary>
        HyperVVirtualMachineShutdown,
      
        /// <summary>
        /// Hyper-V virtual machine started
        /// </summary>
        HyperVVirtualMachineStarted,

        /// <summary>
        /// IIS site failed to register binding (W3SVC event 1007)
        /// </summary>
        IISSiteBindingFailure,
      
        /// <summary>
        /// Hyper-V checkpoint created
        /// </summary>
        HyperVCheckpointCreated,
      
        /// <summary>
        /// IIS website stopped
        /// </summary>
        IISSiteStopped,
      
        /// <summary>
        /// Exchange mailbox database mounted successfully
        /// </summary>
        ExchangeDatabaseMounted,
      
        /// <summary>
        /// DFS Replication partner error
        /// </summary>
        DfsReplicationError,

        /// <summary>
        /// SQL Server database created
        /// </summary>
        SqlDatabaseCreated,

        /// <summary>
        /// Synchronization finished successfully
        /// </summary>
        SyncCompleted,
      
        /// <summary>
        /// Azure AD Connect staging mode enabled
        /// </summary>
        AADConnectStagingEnabled,

        /// <summary>
        /// Azure AD Connect staging mode disabled
        /// </summary>
        AADConnectStagingDisabled,
      
        /// <summary>
        /// Azure AD Connect password synchronization failed
        /// </summary>
        AADConnectPasswordSyncFailed,
      
        /// <summary>
        /// Azure AD Connect run profile completed
        /// </summary>
        AADConnectRunProfile,

        /// <summary>
        /// Azure AD Connect cycle stage (Directory Synchronization 904)
        /// </summary>
        AADSyncCycleStage,

        /// <summary>
        /// Azure AD Connect provision credentials ping (Directory Synchronization 653/654)
        /// </summary>
        AADSyncProvisionCredentialsPing,

        /// <summary>
        /// Password hash sync status heartbeat (Directory Synchronization 663)
        /// </summary>
        AADSyncPasswordHashSyncStatus,

        /// <summary>
        /// Azure AD Connect import/sync status (Directory Synchronization 105/132/133/134)
        /// </summary>
        AADSyncImportStatus,

        /// <summary>
        /// ADSync connector filter status (ADSync 6952)
        /// </summary>
        AADSyncFilterStatus,

        /// <summary>
        /// Network monitor driver loaded (events 6, 7035, 7045)
        /// </summary>
        NetworkMonitorDriverLoaded,

        /// <summary>
        /// Network adapter entered promiscuous mode (events 10400, 10401)
        /// </summary>
        NetworkPromiscuousMode,

        /// <summary>Composite view of Active Directory authentication activity.</summary>
        ActiveDirectoryAuthentication,

        /// <summary>Composite view of Active Directory account and object lifecycle activity.</summary>
        ActiveDirectoryAccountLifecycle,

        /// <summary>Composite view of Active Directory changes, including accounts, objects, and Group Policy.</summary>
        ActiveDirectoryChanges,

        /// <summary>Composite view of Group Policy changes and client processing.</summary>
        GroupPolicyActivity,

        /// <summary>Composite view of Kerberos ticket and policy activity.</summary>
        KerberosActivity,

        /// <summary>Composite view of operating-system startup, shutdown, crash, and time changes.</summary>
        OperatingSystemLifecycle,

        /// <summary>Composite view of Windows security-control changes and sensitive activity.</summary>
        WindowsSecurityChanges,

        /// <summary>Composite view of Microsoft Entra Connect and directory synchronization health.</summary>
        EntraConnectHealth,

        /// <summary>Composite view of network access, firewall, monitoring, and SMB security activity.</summary>
        NetworkSecurity,

        /// <summary>Composite view of infrastructure service health across Windows workloads.</summary>
        InfrastructureHealth,

        /// <summary>A scheduled task was enabled (4700).</summary>
        ScheduledTaskEnabled,
        /// <summary>A scheduled task was disabled (4701).</summary>
        ScheduledTaskDisabled,
        /// <summary>A scheduled task was updated (4702).</summary>
        ScheduledTaskUpdated,
        /// <summary>A Windows Firewall rule was added (4946).</summary>
        FirewallRuleAdded,
        /// <summary>A Windows Firewall rule was deleted (4948).</summary>
        FirewallRuleDeleted,
        /// <summary>Microsoft Defender detected a threat (1116).</summary>
        DefenderThreatDetected,
        /// <summary>Microsoft Defender took action on a threat (1117).</summary>
        DefenderThreatAction,
        /// <summary>Microsoft Defender configuration changed (5007).</summary>
        DefenderConfigurationChanged,
        /// <summary>Composite scheduled-task lifecycle view.</summary>
        ScheduledTaskActivity,
        /// <summary>Composite Windows Firewall rule lifecycle view.</summary>
        FirewallRuleActivity,
        /// <summary>Composite Microsoft Defender security view.</summary>
        DefenderSecurity,
        /// <summary>Composite NTLMv1, weak Kerberos encryption, and unsigned LDAP bind view.</summary>
        AuthenticationHealth,
        /// <summary>Group Policy object, link, inheritance, and WMI-filter directory audit events.</summary>
        GroupPolicyDirectoryAudit,
        /// <summary>KDCsvc RC4 audit, explicit-configuration, and enforcement events.</summary>
        KerberosKdcRc4Audit,
    }
}
