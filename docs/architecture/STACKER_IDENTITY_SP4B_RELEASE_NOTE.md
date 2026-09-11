# SP-4B Release Safety Note

SP-4B is not a production deployment phase.

For any later production release containing SP-4B, the deployment package must preserve the existing production `web.config`. The release process must not overwrite, regenerate, replace, or publish over the production `web.config`.

Required release checks:

1. back up the current production `web.config` before deployment;
2. record its hash and last-modified time;
3. exclude `web.config` from the deployment payload;
4. deploy application binaries and static assets without replacing production configuration;
5. verify the production `web.config` hash remains unchanged after deployment;
6. stop and restore from backup if the file is unexpectedly modified.

This constraint applies to production deployment only and does not authorize any deployment action in SP-4B.
