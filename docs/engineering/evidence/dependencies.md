# Internal C# type mentions and browser script dependencies

| Consumer | Referenced type files or script inputs |
|---|---|
| backend/StackMeet.Api/Activities/ActivityAssignmentPolicy.cs | ActivityModuleContracts, ActivityModuleRegistry, Competition |
| backend/StackMeet.Api/Activities/ActivityModuleContracts.cs | Competition |
| backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs | ActivityAssignmentPolicy, ActivityModuleContracts, ActivityModuleRegistry, ChessActivityModule, CompetitionActivityResolver, SportStackerCareerProfileService, SportStackingActivityModule |
| backend/StackMeet.Api/Activities/ActivityModuleRegistry.cs | ActivityModuleContracts, SportStackingActivityModule |
| backend/StackMeet.Api/Activities/ChessActivityModule.cs | ActivityModuleContracts, SportStackingActivityModule |
| backend/StackMeet.Api/Activities/CompetitionActivityResolver.cs | ActivityModuleContracts, ActivityModuleRegistry, Competition |
| backend/StackMeet.Api/Activities/SportStacking/Identity/IdentityLinkedFinalsPlacementCareerService.cs | FinalsHistoricalPlacementProjectionService, IdentityLinkedTeamCareerService, NadiTrackIdRules, PublicFinalsPlacementCareerIntegrationService, SportStackingActivityModule, StackMeetDbContext, Stacker, StackerIdentityLink, StackerIdentityMatchMethod |
| backend/StackMeet.Api/Activities/SportStacking/Identity/IdentityLinkedTeamCareerService.cs | CompetitionResultRules, CompetitionTeamResultIntegrityService, IdentityLinkedFinalsPlacementCareerService, NadiTrackIdRules, PublicFinalsPlacementCareerIntegrationService, SportStackingActivityModule, StackMeetDbContext, Stacker |
| backend/StackMeet.Api/Activities/SportStacking/Identity/IdentityNameQuality.cs | — |
| backend/StackMeet.Api/Activities/SportStacking/Identity/NadiTrackIdGenerator.cs | NadiTrackIdRules |
| backend/StackMeet.Api/Activities/SportStacking/Identity/NadiTrackIdRules.cs | SportStackingActivityModule |
| backend/StackMeet.Api/Activities/SportStacking/Identity/PublicFinalsPlacementCareerIntegrationService.cs | CompetitionResult, FinalsHistoricalPlacementProjectionService, FinalsRankingCertificationReadinessService, FinalsRankingGovernanceService, IdentityLinkedFinalsPlacementCareerService, IdentityLinkedTeamCareerService, NadiTrackIdRules, PublicFinalsPlacementPublicationContract, StackMeetDbContext, Stacker, StackerIdentityMatchMethod |
| backend/StackMeet.Api/Activities/SportStacking/Identity/PublicFinalsPlacementPublicationContract.cs | Competition, FinalsHistoricalPlacementProjectionService, FinalsRankingCertificationReadinessService, FinalsRankingGovernanceService, IdentityLinkedFinalsPlacementCareerService, SportStackingActivityModule, StackerIdentityMatchMethod |
| backend/StackMeet.Api/Activities/SportStacking/Identity/PublicTeamCareerIntegrationService.cs | IdentityLinkedTeamCareerService, NadiTrackIdRules, PublicTeamCareerPublicationContract, StackMeetDbContext |
| backend/StackMeet.Api/Activities/SportStacking/Identity/PublicTeamCareerPublicationContract.cs | IdentityLinkedTeamCareerService, NadiTrackIdRules, SportStackingActivityModule, StackerIdentityMatchMethod |
| backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs | PublicFinalsPlacementPublicationContract, PublicTeamCareerPublicationContract, SportStackerCareerProfileService, SportStackingActivityModule |
| backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileService.cs | CompetitionResultRules, NadiTrackIdRules, SportStackerCareerProfileModels, StackMeetDbContext |
| backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerIdentity.cs | Stacker |
| backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackingIdentityRegistration.cs | NadiTrackIdGenerator, PublicFinalsPlacementCareerIntegrationService, PublicTeamCareerIntegrationService, StackerIdentityBackfillService |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityBackfillModels.cs | SportStackingActivityModule, StackerIdentityMatchMethod, StackerIdentityMatchModels, StackerIdentityPersistenceModels, StackerIdentityResolutionModels |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityBackfillService.cs | Competition, NadiTrackIdGenerator, NadiTrackIdRules, SportStackerIdentity, StackMeetDbContext, Stacker, StackerIdentityBackfillModels, StackerIdentityMatchModels, StackerIdentityMatcher, StackerIdentityPersistenceModels, StackerIdentityPersistenceService, StackerIdentityResolutionModels, StackerIdentityResolutionPolicy |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityLink.cs | SportStackerIdentity, Stacker |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityMatchMethod.cs | — |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityMatchModels.cs | SportStackerIdentity, StackerIdentityMatchMethod |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityMatcher.cs | NadiTrackIdRules, SportStackerIdentity, StackerIdentityMatchMethod, StackerIdentityMatchModels |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityPersistenceModels.cs | SportStackerIdentity, Stacker, StackerIdentityLink, StackerIdentityResolutionModels |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityPersistenceService.cs | AuditLog, Competition, IdentityNameQuality, NadiTrackIdGenerator, NadiTrackIdRules, SportStackerIdentity, StackMeetDbContext, Stacker, StackerIdentityLink, StackerIdentityMatchMethod, StackerIdentityMatchModels, StackerIdentityMatcher, StackerIdentityPersistenceModels, StackerIdentityResolutionModels |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityResolutionModels.cs | SportStackerIdentity, SportStackingActivityModule |
| backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityResolutionPolicy.cs | NadiTrackIdRules, SportStackerIdentity, StackerIdentityMatchMethod, StackerIdentityMatchModels, StackerIdentityResolutionModels |
| backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsHistoricalPlacementProjectionService.cs | CompetitionResult, CompetitionState, FinalsRankingCertificationReadinessService, FinalsRankingGovernanceService, StackMeetDbContext, Stacker, StackerIdentityMatchMethod |
| backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsRankingCertificationReadinessService.cs | CompetitionState, FinalsRankingGovernanceService, SportStackingActivityModule, StackMeetDbContext, StackerIdentityMatchMethod |
| backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsRankingGovernanceService.cs | Competition, CompetitionState, FinalsRankingCertificationReadinessService, SportStackingActivityModule, StackMeetDbContext, StackerIdentityMatchMethod |
| backend/StackMeet.Api/Activities/SportStackingActivityModule.cs | ActivityModuleContracts |
| backend/StackMeet.Api/Controllers/AdminAuditLogsController.cs | AdminAuditLogDtos, AuditLog, AuditLogService, Competition, SportStackingActivityModule |
| backend/StackMeet.Api/Controllers/AdminCareerProfileCapabilitiesController.cs | — |
| backend/StackMeet.Api/Controllers/AdminEmailSettingsController.cs | AccountEmailService, AccountLinkService, AdminEmailSettingsDtos, AppSetting, AuditLogService, EmailRules, ProtectedSettingService |
| backend/StackMeet.Api/Controllers/AdminStackerIdentitiesController.cs | AuditLogService, Competition, IdentityNameQuality, NadiTrackIdRules, SportStackerIdentity, StackMeetDbContext, Stacker, StackerIdentityBackfillModels, StackerIdentityBackfillService, StackerIdentityLink, StackerIdentityMatchModels, StackerIdentityMatcher, StackerIdentityResolutionModels |
| backend/StackMeet.Api/Controllers/AdminUsersController.cs | AccountEmailService, AccountLinkService, AccountTokenService, AdminUserDtos, AppSetting, AppUser, AuditLogService, Competition, CompetitionUser, EmailRules, PasswordHashService, ProtectedSettingService, SportStackingActivityModule, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/AuthController.cs | AccountEmailService, AccountLinkService, AccountTokenService, AppUser, AppUserToken, AuditLogService, AuthDtos, Competition, CompetitionKeyRules, EmailRules, PasswordHashService, ProtectedSettingService, SessionTokenService, SportStackingActivityModule, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/AuthSessionsController.cs | AppUser, AuditLogService, SessionToken, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/CompetitionAdminController.cs | AuditLogService, Competition, CompetitionAdminDtos, CompetitionKeyRules, CompetitionState, CompetitionStateResetService, EmptyCompetitionStateFactory, PasswordHashService, ResultsHub, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/CompetitionAssetsController.cs | AuditLogService, Competition, CompetitionAsset, CompetitionAssetDtos, CompetitionAssetStorage, CompetitionPermissionService, SessionToken, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/CompetitionResultsController.cs | Competition, CompetitionPermissionService, CompetitionResult, CompetitionResultDtos, CompetitionResultRules, CompetitionTeamResultIntegrityService, ResultsHub, SessionToken, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/CompetitionStateController.cs | Competition, CompetitionKeyRules, CompetitionParticipantReferenceService, CompetitionPermissionService, CompetitionState, CompetitionTeamResultIntegrityService, ResultsHub, SessionToken, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/CompetitionsController.cs | ActivityAssignmentPolicy, ActivityModuleContracts, Competition, CompetitionActivityDtos, CompetitionActivityResolver, CompetitionDtos, CompetitionKeyRules, SessionToken, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/FinalsRankingRulesController.cs | FinalsRankingGovernanceService, SessionToken, StackMeetDbContext |
| backend/StackMeet.Api/Controllers/HealthController.cs | — |
| backend/StackMeet.Api/Controllers/PublicCompetitionDirectoryController.cs | StackMeetDbContext |
| backend/StackMeet.Api/Controllers/PublicResultsController.cs | StackMeetDbContext, Stacker |
| backend/StackMeet.Api/Controllers/PublicResultsProfileLinksController.cs | NadiTrackIdRules, SportStackerIdentity, StackMeetDbContext, Stacker |
| backend/StackMeet.Api/Controllers/PublicStackerProfilesController.cs | NadiTrackIdRules, PublicFinalsPlacementCareerIntegrationService, PublicTeamCareerIntegrationService, SportStackerCareerProfileModels, SportStackerCareerProfileService, Stacker |
| backend/StackMeet.Api/Controllers/StackersController.cs | CompetitionParticipantReferenceService, CompetitionPermissionService, SessionToken, StackMeetDbContext, Stacker, StackerDtos |
| backend/StackMeet.Api/Controllers/VersionController.cs | — |
| backend/StackMeet.Api/Data/StackMeetDbContext.cs | AppRole, AppSetting, AppUser, AppUserToken, AuditLog, Competition, CompetitionAsset, CompetitionResult, CompetitionState, CompetitionUser, SportStackerIdentity, StackMeetRoles, Stacker, StackerIdentityLink |
| backend/StackMeet.Api/Dtos/AdminAuditLogDtos.cs | — |
| backend/StackMeet.Api/Dtos/AdminEmailSettingsDtos.cs | — |
| backend/StackMeet.Api/Dtos/AdminUserDtos.cs | Competition, SportStackingActivityModule |
| backend/StackMeet.Api/Dtos/AuthDtos.cs | — |
| backend/StackMeet.Api/Dtos/CompetitionActivityDtos.cs | — |
| backend/StackMeet.Api/Dtos/CompetitionAdminDtos.cs | — |
| backend/StackMeet.Api/Dtos/CompetitionAssetDtos.cs | — |
| backend/StackMeet.Api/Dtos/CompetitionDtos.cs | — |
| backend/StackMeet.Api/Dtos/CompetitionResultDtos.cs | — |
| backend/StackMeet.Api/Dtos/StackerDtos.cs | — |
| backend/StackMeet.Api/Hubs/ResultsHub.cs | — |
| backend/StackMeet.Api/Models/AppRole.cs | CompetitionUser, StackMeetDbContext |
| backend/StackMeet.Api/Models/AppSetting.cs | — |
| backend/StackMeet.Api/Models/AppUser.cs | AppUserToken, CompetitionUser |
| backend/StackMeet.Api/Models/AppUserToken.cs | AppUser |
| backend/StackMeet.Api/Models/AuditLog.cs | AppUser, Competition |
| backend/StackMeet.Api/Models/Competition.cs | AuditLog, CompetitionAsset, CompetitionResult, CompetitionUser, Stacker |
| backend/StackMeet.Api/Models/CompetitionAsset.cs | Competition |
| backend/StackMeet.Api/Models/CompetitionResult.cs | Competition |
| backend/StackMeet.Api/Models/CompetitionState.cs | — |
| backend/StackMeet.Api/Models/CompetitionUser.cs | AppRole, AppUser, Competition |
| backend/StackMeet.Api/Models/Stacker.cs | Competition, StackerIdentityLink |
| backend/StackMeet.Api/Program.cs | AccountEmailService, AccountLinkService, AccountTokenService, AuditLogService, Competition, CompetitionAssetStorage, CompetitionParticipantReferenceService, CompetitionPermissionService, CompetitionTeamResultIntegrityService, PasswordHashService, ProtectedSettingService, ResultsHub, SessionToken, SessionTokenService, StackMeetDbContext |
| backend/StackMeet.Api/Services/AccountEmailService.cs | ProtectedSettingService |
| backend/StackMeet.Api/Services/AccountLinkService.cs | — |
| backend/StackMeet.Api/Services/AccountTokenService.cs | AppUserToken, StackMeetDbContext |
| backend/StackMeet.Api/Services/AuditLogService.cs | AuditLog, Competition, SessionToken, SessionTokenService, StackMeetDbContext |
| backend/StackMeet.Api/Services/CompetitionAssetStorage.cs | Competition |
| backend/StackMeet.Api/Services/CompetitionKeyRules.cs | — |
| backend/StackMeet.Api/Services/CompetitionParticipantReferenceService.cs | — |
| backend/StackMeet.Api/Services/CompetitionPermissionService.cs | Competition, StackMeetDbContext, StackMeetRoles |
| backend/StackMeet.Api/Services/CompetitionResultRules.cs | — |
| backend/StackMeet.Api/Services/CompetitionStateResetService.cs | — |
| backend/StackMeet.Api/Services/CompetitionTeamResultIntegrityService.cs | Competition, CompetitionResultDtos, CompetitionResultRules, StackMeetDbContext, Stacker |
| backend/StackMeet.Api/Services/DailyAuditReportService.cs | AccountEmailService, AuditLog, Competition, StackMeetDbContext |
| backend/StackMeet.Api/Services/EmailRules.cs | SportStackingActivityModule |
| backend/StackMeet.Api/Services/EmptyCompetitionStateFactory.cs | — |
| backend/StackMeet.Api/Services/PasswordHashService.cs | — |
| backend/StackMeet.Api/Services/ProtectedSettingService.cs | AppSetting, StackMeetDbContext |
| backend/StackMeet.Api/Services/SessionToken.cs | — |
| backend/StackMeet.Api/Services/SessionTokenService.cs | SessionToken |
| backend/StackMeet.Api/Services/StackMeetRoles.cs | AppRole |
| backend/StackMeet.Api/wwwroot/account.html | js/i18n/I18n.js?v=multilanguage-v2-phase3b, js/i18n/locales/en.js?v=multilanguage-v2-phase3b, js/i18n/locales/ms.js?v=multilanguage-v2-phase3b, js/i18n/locales/zh-Hans.js?v=multilanguage-v2-phase3b, js/i18n/LanguagePreference.js?v=multilanguage-v2-phase3b, js/i18n/UiLocalization.js?v=multilanguage-v2-phase3b, account.js?v=multilanguage-v2-phase3b |
| backend/StackMeet.Api/wwwroot/admin.html | js/i18n/I18n.js?v=multilanguage-v2-phase3b, js/i18n/locales/en.js?v=multilanguage-v2-phase3b, js/i18n/locales/ms.js?v=multilanguage-v2-phase3b, js/i18n/locales/zh-Hans.js?v=multilanguage-v2-phase3b, js/i18n/LanguagePreference.js?v=multilanguage-v2-phase3b, js/i18n/UiLocalization.js?v=multilanguage-v2-phase3b, career-admin.js?v=career-admin-v1, admin.js?v=career-admin-v1 |
| backend/StackMeet.Api/wwwroot/all/index.html | /all/all.js?v=20260731-public-directory |
| backend/StackMeet.Api/wwwroot/index.html | config.js, js/i18n/I18n.js?v=multilanguage-v2-phase3b, js/i18n/locales/en.js?v=multilanguage-v2-phase3b, js/i18n/locales/ms.js?v=multilanguage-v2-phase3b, js/i18n/locales/zh-Hans.js?v=multilanguage-v2-phase3b, js/i18n/LanguagePreference.js?v=multilanguage-v2-phase3b, js/i18n/UiLocalization.js?v=multilanguage-v2-phase3b, js/auth/AuthSession.js?v=multilanguage-v2-phase3b, js/storage/ApiProvider.js?v=20260714-doubles4, js/storage/StackerApi.js?v=20260714-doubles4, js/storage/CompetitionAssetApi.js?v=competition-assets-ui-1, js/storage/ResultApi.js?v=20260817-results-phase2a, js/storage/Repository.js?v=competition-sync-20260803b, js/results/BestResultEngine.js?v=20260722-report-engine-fix, js/reports/FinalsReportEngine.js?v=20260722-report-engine-fix, js/CompetitionPackage.js?v=20260803-package-v1, js/CompetitionPackageAssembler.js?v=20260803-package-assembler-v1, results/vendor/signalr.min.js?v=20260803-live-sync, app.js?v=multilanguage-v2-phase3b |
| backend/StackMeet.Api/wwwroot/profile/index.html | /profile/profile.js |
| backend/StackMeet.Api/wwwroot/results/index.html | /js/i18n/I18n.js?v=multilanguage-v2-phase3c, /js/i18n/locales/en.js?v=multilanguage-v2-phase3c, /js/i18n/locales/ms.js?v=multilanguage-v2-phase3c, /js/i18n/locales/zh-Hans.js?v=multilanguage-v2-phase3c, /results/vendor/signalr.min.js, /results/results.js?v=multilanguage-v2-phase3c, /results/profile-links.js?v=sp4o-20260917a |
