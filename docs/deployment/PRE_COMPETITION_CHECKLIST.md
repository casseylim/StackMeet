# Pre-competition checklist

## One to three days before

- [ ] Deploy the tested runtime file set to the production HTTPS URL.
- [ ] Complete the production smoke test in Edge/Chrome on each operator workstation.
- [ ] Use the production HTTPS hostname from now on. Do not seed the competition on HTTP, localhost, a staging hostname, or `file://` and expect that data to appear in production.
- [ ] Select one named primary operator browser profile for live data entry.
- [ ] Create a verified baseline XML export from that profile; save it outside the public web root in an access-controlled location.
- [ ] Verify a second person can import a copy of the baseline XML into a separate test browser profile.
- [ ] Confirm each printer, page size, orientation, margins, and print preview for reports and time sheets.
- [ ] Confirm downloads are allowed by the browser and security software for XML, CSV, JSON, and Excel-compatible `.xls` exports.
- [ ] Disable browser data-clearing policies, profile cleanup, private/incognito mode, and automatic cache/profile reset for the live operator profile.
- [ ] Record the production URL, primary operator, backup operator, and XML backup location.

## Immediately before competition opens

- [ ] Open the production URL and confirm the HTTPS certificate is valid.
- [ ] Confirm logo, banner, and dashboard render without failed-network requests.
- [ ] Confirm the competition settings, events, divisions, and registrations in the primary browser profile.
- [ ] Export an XML checkpoint and label it with date and time.
- [ ] Run one non-destructive report and one print-preview test.
- [ ] Confirm no other workstation will make concurrent edits to a separate local copy.

## During competition

- [ ] Use the designated primary browser profile for all authoritative data entry.
- [ ] Export XML after registration closes, at scheduled breaks, before finals, and after final results; keep multiple timestamped copies.
- [ ] After every export, confirm the browser download completed and the XML file is non-empty.
- [ ] Before an operator handover, export XML from the current machine, import it into the replacement machine, then verify participant and result counts before continuing.
- [ ] Use normal browser print preview before printing a production batch.
- [ ] Do not clear browsing data, change hostname, change browser profile, use private mode, or rely on a second machine as a live replica.

## After competition

- [ ] Export and retain a final XML archive.
- [ ] Export final reports required by the organizer.
- [ ] Store the final XML and exports outside the web root with the event records.
- [ ] Record any browser, printer, or hosting issues for the later API/database migration.

