<p align="center">
  <a href="https://www.powershellgallery.com/packages/PSWinReportingV2"><img src="https://img.shields.io/powershellgallery/v/PSWinReportingV2.svg"></a>
  <a href="https://www.powershellgallery.com/packages/PSWinReportingV2"><img src="https://img.shields.io/powershellgallery/v/PSWinReportingV2.svg?label=powershell%20gallery%20preview&colorB=yellow&include_prereleases"></a>
  <a href="https://github.com/EvotecIT/PSWinReporting"><img src="https://img.shields.io/github/license/EvotecIT/PSWinReporting.svg"></a>
</p>

<p align="center">
  <a href="https://www.powershellgallery.com/packages/PSWinReportingV2"><img src="https://img.shields.io/powershellgallery/p/PSWinReportingV2.svg"></a>
  <a href="https://github.com/EvotecIT/PSWinReporting"><img src="https://img.shields.io/github/languages/top/evotecit/PSWinReporting.svg"></a>
  <a href="https://github.com/EvotecIT/PSWinReporting"><img src="https://img.shields.io/github/languages/code-size/evotecit/PSWinReporting.svg"></a>
  <a href="https://www.powershellgallery.com/packages/PSWinReporting"><img src="https://img.shields.io/powershellgallery/dt/PSWinReporting.svg?label=downloads%20PSWinReporting"></a>
  <a href="https://www.powershellgallery.com/packages/PSWinReportingV2"><img src="https://img.shields.io/powershellgallery/dt/PSWinReportingv2?label=downloads%20PSWinReportingV2"></a>
</p>

<p align="center">
  <a href="https://twitter.com/PrzemyslawKlys"><img src="https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social"></a>
  <a href="https://evotec.xyz/hub"><img src="https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg"></a>
  <a href="https://www.linkedin.com/in/pklys"><img src="https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn"></a>
</p>

# PSWinReporting

**PSWinReporting** is a little PowerShell module that solves the problem of monitoring and reading **Windows Events**. It allows you to set up monitoring of **Domain Controllers** (and from **2.X** any other servers) for events that happen on them. By default, it comes with **built-in Active Directory** events supports, but since **2.0** you can configure it to monitor anything. You can set up reporting on any types of events and have emails delivered with a summary of hourly, daily, weekly, monthly, or quarterly changes. It also supports sending notifications to Microsoft Teams, Slack, and Discord. Make sure to go thru related articles as they have all the KNOW HOW which is quite useful if you want to get everything from this module.

The full project description is available on my website - [Full project description](https://evotec.xyz/hub/scripts/pswinreporting-powershell-module/).

Currently, there are 2 branches of PSWinReporting.

- [x] Legacy branch - available in PS Gallery as [PSWinReporting](https://www.powershellgallery.com/packages/PSWinReporting/) - `Install-Module -Name 'PSWinReporting' -Force`
- [x] Master branch - available in PS Gallery as [PSWinReportingV2](https://www.powershellgallery.com/packages/PSWinReportingV2/) - `Install-Module -Name 'PSWinReportingV2' -Force`

I've decided that both PowerShell modules can coexist together, especially for scenarios for people who want to switch, but don't want to do it right away. This way, you can keep using old version as is, and slowly fix your other stuff, or use new `Find-Events` command. I've slightly renamed the commands for V2 release.

## PSWinReportingV2 - Master Edition

Master edition is a complete rewrite and a new beginning. It provides the same functionality as **Legacy 1.X** version and then some more.

- [x] Ability to translate report and have it suite your needs
- [x] Ability to completely modify events monitoring
- [x] Ability to monitor any servers, for any events using simple to use schema
- [x] Ability to target multiple servers, computers or files at the same time


At this moment there is no documentation for PSWinReportingV2 except for those articles below. Feel free to explore Examples if you're eager to try the new version — otherwise fallback to PSWinReporting **Legacy Edition**.

- [x] [Find-Events - The only PowerShell Command you will ever need to find out who did what in Active Directory](https://evotec.xyz/the-only-powershell-command-you-will-ever-need-to-find-out-who-did-what-in-active-directory/)

### Built-in Active Directory Reports

PSWinReporting comes with predefined, built-in reports. Those are for `Find-Events`. Those also come defined in example configuration script which you can use straight away after verifying everything is as per your requirement.

- [x] ADComputerChangesDetailed
- [x] ADComputerCreatedChanged
- [x] ADComputerDeleted
- [x] ADGroupChanges
- [x] ADGroupChangesDetailed
- [x] ADGroupCreateDelete
- [x] ADGroupEnumeration
- [x] ADGroupMembershipChanges
- [x] ADGroupPolicyChanges
- [x] ADLogsClearedOther
- [x] ADLogsClearedSecurity
- [x] ADUserChanges
- [x] ADUserChangesDetailed
- [x] ADUserLockouts
- [x] ADUserLogon
- [x] ADUserLogonKerberos
- [x] ADUserStatus
- [x] ADUserUnlocked
- [X] ADOrganizationalUnitChangesDetailed (added in 2.0.10)
- [x] OSStartupShutdownCrash (added in 2.0.12) - covers startup, shutdown and crashes - probably needs some work on the engine later on to allow field merging
- [x] OSCrash (added in 2.0.12) - covers system crashes
- [x] NetworkAccessAuthenticationPolicy (added in 2.0.12) - covers authorizations approved/denied for WIFI and ETHERNET

### Built-in Reporting Times

PSWinReporting comes with predefined report times. This means you can use **True/False** to enable/disable period. In case of `Find-Events`, you can use defined times (checked only) from **DatesRange** parameter.

- [ ] CurrentDay
- [ ] CurrentDayMinusDayX
- [ ] CurrentDayMinuxDaysX
- [x] CurrentHour
- [x] CurrentMonth
- [x] CurrentQuarter
- [ ] CustomDate
- [x] Everything
- [x] Last14days
- [x] Last3days
- [x] Last7days
- [ ] OnDay
- [x] PastDay
- [x] PastHour
- [x] PastMonth
- [x] PastQuarter

Of course, you can also define **DateFrom**, **DateTo** parameters for custom use when using `Find-Events` command.

## PSWinReporting - Legacy Edition

***Legacy edition*** will continue it's life as ***1.X.X***. If you want to keep on using it, feel free, but it's highly encouraged to use ***2.x.x*** when it's fully functional with all features. Code is available as [Legacy Branch](https://github.com/EvotecIT/PSWinReporting/tree/Legacy). Following links can help in understanding how it works and how to set it up:

- [Review of features coming in 2.0 along with some features description for the current version](https://evotec.xyz/pswinreporting-1-8-split-of-branches-legacy-vs-new-hope/) - Overview of configuration and features.
- [Review of new features in PSWinReporting 1.7](https://evotec.xyz/pswinreporting-forwarders-microsoft-teams-slack-microsoft-sql-and-more/) - Lots of changes, review required. Microsoft Teams, Slack, SQL and forwarders support
- [Review of new features in PSWinReporting 1.0](https://evotec.xyz/pswinreporting-1-0-is-out/) - Lots of changes, review required.
- [Last version of Get-EventsLibrary.ps1](https://evotec.xyz/get-eventslibrary-ps1-monitoring-events-powershell/) - This is the actual code base for the old version. Just in caseâ€¦
- [Blog post about version 0.8](https://evotec.xyz/whats-new-event-monitoring-0-8/) - Updates from feedback. Last version before the name change.
- [Blog post about version 0.7](https://evotec.xyz/whats-new-event-monitoring-v0-7/) - Updates from feedback.
- [Blog post about version 0.6](https://evotec.xyz/whats-new-event-monitoring-v0-6/) - Updates from feedback.
- [Blog post about initial version and the differences in monitoring approach](https://evotec.xyz/monitoring-active-directory-changes-on-users-and-groups-with-powershell/)

Following AD Events are supported:

- [x] Group create, delete, modify (Who / When / What)
- [x] Group membership changes (Who / When / What)
- [x] User changes (Who / When / What)
- [x] User created / deleted (Who / When)
- [x] User password changes (Who / When)
- [x] User lockouts (Who / When / Where)
- [x] Computer Created / Modified (Who / When / Where)
- [x] Computer Deleted (Who / When / Where)
- [x] Event Log Backup (Who / When)
- [x] Event Log Clear (Who / When)

Features:

- [x] Support for Event Forwarding - monitoring one event log instead of scanning all domain controllers
- [x] Support for Microsoft Teams - Sending events as they happen to Microsoft Teams (only supported when forwarders are in use)
- [x] Support for Slack - Sending events as they happen to Slack (only supported when forwarders are in use)
- [x] Support for Microsoft SQL - Sending events directly to SQL (some people prefer it that way)
- [x] Support for backing up old archived logs (moves logs from Domain Controllers into chosen place)
- [x] Support for re-scanning logs from files - a way to recheck your logs for missing information

### Example - Script running

![image](https://evotec.xyz/wp-content/uploads/2018/06/2018-06-10_11-20-08.gif.pagespeed.ce.xrLSOGTIkk.gif)

### Example - Email Report

![image](https://evotec.xyz/wp-content/uploads/2018/06/PSWinReporting1.0-Example1.png)

### Example - Microsoft Teams

![image](https://evotec.xyz/wp-content/uploads/2018/09/img_5b9e830101081.png)

### Example - Slack

![image](https://evotec.xyz/wp-content/uploads/2018/09/img_5b9e7041638f5.png)
