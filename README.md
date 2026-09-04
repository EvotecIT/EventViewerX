<p align="center">
  <a href="https://www.powershellgallery.com/packages/PSWinReporting"><img src="https://img.shields.io/powershellgallery/v/PSWinReporting.svg"></a>
  <a href="https://www.powershellgallery.com/packages/PSWinReporting"><img src="https://img.shields.io/powershellgallery/vpre/PSWinReporting.svg?label=powershell%20gallery%20preview&colorB=yellow"></a>
  <a href="https://github.com/EvotecIT/PSWinReporting"><img src="https://img.shields.io/github/license/EvotecIT/PSWinReporting.svg"></a>
</p>

<p align="center">
  <a href="https://www.powershellgallery.com/packages/PSWinReporting"><img src="https://img.shields.io/powershellgallery/p/PSWinReporting.svg"></a>
  <a href="https://github.com/EvotecIT/PSWinReporting"><img src="https://img.shields.io/github/languages/top/evotecit/PSWinReporting.svg"></a>
  <a href="https://github.com/EvotecIT/PSWinReporting"><img src="https://img.shields.io/github/languages/code-size/evotecit/PSWinReporting.svg"></a>
  <a href="https://www.powershellgallery.com/packages/PSWinReporting"><img src="https://img.shields.io/powershellgallery/dt/PSWinReporting.svg"></a>
</p>

<p align="center">
  <a href="https://twitter.com/PrzemyslawKlys"><img src="https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social"></a>
  <a href="https://evotec.xyz/hub"><img src="https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg"></a>
  <a href="https://www.linkedin.com/in/pklys"><img src="https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn"></a>
</p>

# PSWinReporting - Legacy Edition

PSWinReporting is frozen for existing deployments. Version 1.8.1.7 is the final
compatibility release from the
[`PSWinReporting`](https://github.com/EvotecIT/EventViewerX/tree/PSWinReporting)
branch. New event-query, reporting, and automation development belongs to
[`EventViewerX`](https://github.com/EvotecIT/EventViewerX); PowerShell users
consume that maintained engine through the `PSEventViewer` module.

This `PowerShell Module`, which started as an event library `(Get-EventsLibrary.ps1)`, has now grown up and became full fledged PowerShell Module. This module has multiple functionalities but one of the signature features of this module is ability to parse Security (mostly) logs on `Domain Controllers`. But that's not all. You can set up reporting on it and have emails delivered with summary of **hourly**, **daily**, **weekly**, **monthly** or **quarterly** changes. Changes that happen on your **Active Directory** Domain. Changes that your Service Desk agents, or other administrators do. And with new versions… well you can do a lot of stuff. Just read below. Make sure to go thru related articles as they have all the KNOW HOW which is quite useful if you want to get everything from this module. Module is published on [Powershell Gallery](https://www.powershellgallery.com/packages/PSWinReporting/).

Existing installations can remain on PSWinReporting when migration is not yet
practical. Treat this branch as a compatibility surface rather than a second
place for feature development.

## Maintainer release

`Build\Manage-Module.ps1 -RunMode Build` creates unpacked and packed artifacts
without publishing. The `Publish` gate publishes the module to PowerShell
Gallery and creates its namespaced GitHub release. It reads credentials from
`PSGALLERY_API_KEY` and `GITHUB_TOKEN`; signing remains controlled by
`-SignModule`.

## ChangeLog

- 1.8.1.7 - Final compatibility release
  - Keeps the compatible event-query implementation inside the frozen module.
  - Locks gallery dependencies to the versions used by this release line.
  - Uses the shared PSPublishModule build and publication pipeline.
- 1.8.1.6 - 25.09.2021
  - Now checks for LogFile path existance before continuing.
  - Now handles errors when dealing with event log size
  - Fixes some outstanding issues
- 1.8.1.5 - 30.01.2020
  - Fix for RGBColors to support newer PSTeams module

- 1.8.1.4 - 11.01.2020
  - Update of dependencies
  - Fix for SQL issues

- 1.8.1.2 - 22.05.2019
  - Fixed problem with Template for Forwarding Collector
- 1.8.1.1 - 19.03.2019
  - Fixed IPV6 display
- 1.8.1 - 18.03.2019
  - Added SendMailOnlyOnEvents feature.
  - Fixed SQL Server issue when DB name was containing `-` (Update-Module PSSharedGooods is required)
- 1.8.0 - 10.03.2019
  - Update to configuration and overview of some features
  - All information provied in blog post: <https://evotec.xyz/pswinreporting-1-8-split-of-branches-legacy-vs-new-hope/>
- 1.7.7 - 21.02.2019
  - Fix for long RecordID
  - Fix of colors for Teams
  - Fix for Teams Summary visible in activity pane
- 1.7.6 - XX.02.2019
  - Fixes for PDC detection
  - Some optimization
  - Bundle as single PSM1 file (speed up loading process)

## Links

- [Documentation for PSWinReporting (module description, installation, how to)](https://evotec.xyz/hub/scripts/pswinreporting-powershell-module/) - Full project description
- [Review of features coming in 2.0 along with some features description for the current version](https://evotec.xyz/pswinreporting-1-8-split-of-branches-legacy-vs-new-hope/) - Nice overview
- [Review of new features in PSWinReporting 1.7](https://evotec.xyz/pswinreporting-forwarders-microsoft-teams-slack-microsoft-sql-and-more/) - Lots of changes, review required. Microsoft Teams, Slack, SQL and forwarders support
- [Review of new features in PSWinReporting 1.0](https://evotec.xyz/pswinreporting-1-0-is-out/) - Lots of changes, review required.
- [Last version of Get-EventsLibrary.ps1](https://evotec.xyz/get-eventslibrary-ps1-monitoring-events-powershell/) - This is actual code base for the old version. Just in case…
- [Blog post about version 0.8](https://evotec.xyz/whats-new-event-monitoring-0-8/) - Updates from feedback. Last version before name change.
- [Blog post about version 0.7](https://evotec.xyz/whats-new-event-monitoring-v0-7/) - Updates from feedback.
- [Blog post about version 0.6](https://evotec.xyz/whats-new-event-monitoring-v0-6/) - Updates from feedback.
- [Blog post about initial version and the differences in monitoring approach](https://evotec.xyz/monitoring-active-directory-changes-on-users-and-groups-with-powershell/)

## Project Features

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
