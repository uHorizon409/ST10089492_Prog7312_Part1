# Iterative Feedback Incorporation

Tracking updates made in response to actual lecturer feedback received on earlier parts of the Smart-X project, not a generic list of self identified improvements.

## Feedback received on Part 1 (Formative 1)

Two rubrics were returned for Part 1: the PROG7312 Part 1 content rubric (89/100) and the IIE Technical Referencing Rubric (minor inconsistencies and incongruences flagged).

### Documentation and Readme Quality, 4/5

Feedback: "readme provided outlines settings steps required to configure, run dependencies, run the multi stack toolset. Little to no Markdown included."

Update made: the README was rebuilt with proper Markdown structure for both Part 2 and Part 3, headed sections, fenced code blocks for every terminal command, bullet lists mapping each brief requirement to the file that implements it, and numbered step by step testing instructions for every new data structure added in each part. This is a direct response to the "little to no Markdown" note, not a general tidy up.

### Gateway Startup Layout and API Integration, 9/10

Feedback: top band language was "Flawless startup experience, Asynchronous .NET API communication is clean, highly responsive and robust", the mark awarded sat one band below that.

Update made: Part 2 changed the main menu from `ShowDialog()`, which blocked the parent window and forced a close-then-reopen cycle to switch between pillars, to a non-modal `Show()` call with single instance tracking, so every pillar can be open and updating live at the same time. Part 3 followed the exact same pattern for the new Network Topology page rather than requiring a second redesign.

### User Interface Design Considerations, 8/10

Feedback: interface was "mostly consistent" rather than visually outstanding.

Update made: the two, then three, pillar windows originally landed exactly on top of each other when opened together, this was fixed by giving each pillar a manual offset position. A genuine UI bug was also found and fixed during Part 2 testing, the Command Stream page's status label had no explicit height, so a two line error message got visually clipped, this was corrected by giving the label a fixed height and repositioning it.

### App Functionality: Sensor Telemetry Data Ingestion and UI, 9/10

Feedback: "minor UI quirks or minor validation gaps" noted against the top band.

Update made: Part 2 added server side validation that was missing in Part 1, `CommandsController` now rejects a manual override command (`TriggerPump`, `ToggleRelay`) targeted at a device that is not an Actuator, returning a clear error instead of silently accepting it.

### Referencing, IIE Technical Referencing Rubric

Feedback: minor inconsistencies in the in-text reference format, and minor incongruence between in-text citations and the bibliography, specifically one or two sources appearing in one list but not the other.

Update made: the Part 3 Project Completion Report and Technology Recommendations sections use one consistent `(Author, Year)` in-text format throughout, and every in-text citation has a matching entry in the reference list and vice versa, checked directly against that specific feedback before submission.

### Not yet addressed

Feedback: "Media / Log File Upload Attachment, 7/10, file uploader is present but exhibits notable usability bugs when saving configs or uploading images to the backend API."

Status: this was not revisited in Part 2 or Part 3, neither part touched `AttachmentsController` or the client side `OpenFileDialog` flow, since the brief for both parts was scoped to telemetry processing and mesh routing rather than the file upload feature. Noted honestly here rather than claiming it was fixed.

## Feedback received on Part 2

Part 2 scored 89/100. Referencing came back "Technically correct referencing" overall despite the same minor inconsistency/incongruence notes as Part 1, no deduction applied.

### Main Menu Navigation and Active Pillar State, 26/30

Feedback: band language noted was "Menu functions well, providing smooth transition between Part 1 and Part 2 views with minor state-tracking quirks."

Update made: Part 3 extended the exact same non-modal, single instance tracked pattern to the third pillar (`NetworkTopologyForm`), rather than introducing a different approach for the new page. Whether the specific state-tracking quirk the marker saw has actually gone away still needs confirming with a live Windows run through of all three pillars open at once, this has not been visually confirmed since this build was done from WSL, which has no Windows GUI to run the client in.

### Lookup Structures: Dictionaries and Key-Value Maps, 13/15

Feedback: band language noted was "Seamless integration; device caching instantaneous, timelines load instantly via highly optimised sorted structures," one band below the mark awarded suggests.

Update made: `TimestampedLogStore.GetTimelineFor` was still doing a linear scan across every sensor's combined history on every read. Part 3 added `SensorHistoryAvlTree.cs`, a real self balancing tree with one instance per sensor, so a range query for one sensor's history no longer touches another sensor's data, or the rest of its own history outside the requested range, at all.

### Advanced Collections and Unique Collections, 14/15 and 9/10

Both close to full marks with no specific critique text attached, no targeted change was made against these for Part 3, both structures were left as built and verified in Part 2.

### GitHub Usage, 0/0

Feedback: "excellent commit history (25+ commits) and well structured commit messages. Evidence of collaboration (multiple contributors). README and file included and perfectly formatted in Markdown. No deduction."

Status: confirmed correct, this practice was continued unchanged into Part 3, granular single purpose commits and a Markdown formatted README kept up to date with every new data structure.
