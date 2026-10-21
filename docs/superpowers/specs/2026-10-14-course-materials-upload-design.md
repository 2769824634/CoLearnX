# Course materials upload

Creator uploads materials from an existing course page. The standalone Upload Material entry is removed.

## Flow

1. Creator opens Courses and opens one course.
2. The course page keeps its current definition:
   - Draft or Rejected: the existing editor, reduced to two steps (basic information, then interests and price).
   - Pending approval or Published: the existing read-only definition.
3. Under that definition, every existing course shows a Course materials card listing the files already on the course. The Upload materials button sits in the course header, directly under the status, so it is visible without scrolling.
4. The button opens a modal. The creator chooses one or more files, edits each title, sets one category and one optional description for the batch, and submits.
5. Each file is uploaded to the current course. On full success the modal closes, the list refreshes, and the page shows Uploaded. A course that already has files keeps those files and accepts more.

A new course at `/creator/courses/new` still uses the three-step wizard. Step 2 still lets the creator pick files that upload when the course is saved. That page has no Upload materials button, because the course does not exist yet.

## Page

- Remove Upload Material from the creator sidebar.
- Remove the Upload Material quick action on the creator home page. Usage Records stays.
- `/creator/upload` redirects to `/creator/courses`. The standalone upload page is removed.
- The Course materials card uses the existing card and `MaterialList`. Upload materials is a primary button in the course header, under the status.
- The card is shown for Draft, Rejected, Pending approval, and Published courses owned by the creator.

## Modal

Use the existing `Modal`.

- Title: Upload materials.
- Close with the close button, Cancel, Escape, or the overlay, while no upload is in progress.
- While an upload is in progress, the form, Cancel, close, Escape, and overlay do nothing. The submit button reads Uploading….
- Fields use the existing `form-group` styles.
- Category is one choice for the whole batch: Programming, Design, or Business. It starts as the course category when that is one of those choices, and otherwise starts as Design.
- Description is one optional text area for the whole batch. A blank description is omitted on each request.
- The file input accepts multiple PDF, PPTX, DOCX, PNG, and JPG files. Each file may be up to 20 MB.
- Chosen files appear in a list with the filename, size, an editable title, and Remove. The title starts as the filename without its extension. When that leaves nothing, the title starts as Course material. An empty title blocks submit and shows Title is required on that row.
- At least one file is required. The file error is Choose a file to upload.
- Submit reads Upload.

## Upload

No new API. For each remaining file, in list order, call the existing `POST /api/materials` once with that file’s title, the shared category, the shared description, the file, and the current course id.

- When every file succeeds, close the modal, reload the course library, and show the existing Uploaded toast.
- When a file fails, reload the library so earlier files in that batch appear there, remove those successful files from the modal, keep the failed file and the files after it, and show the request error in the modal. The creator can fix the batch and submit again.
- The server keeps its current rules: the creator must own the course, the title is required, and each file must be a supported type of at most 20 MB. Course status does not block upload.

## Out of scope

- Trainer session materials.
- Changing material review, download, or cloud-link behavior.
- A multi-file API.
- Editing or deleting a material from this card.

## Tests

- An existing course in each status shows Course materials, its current files, and Upload materials.
- The button opens the modal. Two selected files submit two uploads, in order, with the current course id, edited titles, the shared category, and the shared description.
- A failure on the second file leaves the first file in the library, keeps the second file in the modal, and shows the error.
- The creator sidebar and home page no longer link to `/creator/upload`. That path redirects to `/creator/courses`.
- The new-course wizard still collects files on step 2 and uploads them after the course is created.
