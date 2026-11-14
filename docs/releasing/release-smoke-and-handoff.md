# Release preparation and publication

A version tag starts the **prepare** job. The job checks the exact tag and commit, then builds and tests the solution, validates the package and documentation archives, and runs the offline browser smoke. Download **release-candidate** from the successful run. Its manifest lists the SHA-256 and length of every candidate file and records the version from the VSIX. **release-browser-smoke** contains the browser report and rendered pages.

The browser smoke uses Node 22 or later and a local Chromium browser. It opens the extracted offline site on loopback, follows the landing-page links to the user guide and API, checks rendered headings, styles and logo, and saves screenshots. Pass the browser executable explicitly with **-BrowserPath** when it is installed outside the standard Windows locations.

## Observe the candidate

| Gate | Evidence required |
| --- | --- |
| Visual Studio | Run **scripts/Test-ReleaseDesigner.ps1** with the candidate VSIX, its SHA-256, VSIX version, tag, commit and a fresh output directory. Use an interactive Windows validation environment with a single Visual Studio installation, the extensibility SDK and the IDE closed. The script opens the fixture in a fresh experimental profile and records host readiness. Inspect the loaded Designer and record the visual result before closing that instance. |
| Android | Run the [Android release matrix](../android-release-validation.md) with the prepared **ModernFormsNextSample.apk** on the selected physical device. Keep the matrix and device observations together. Reference that evidence and the exact APK digest in the approval. |
| Marketplace | Confirm the prepared VSIX is approved for the Marketplace handoff and record whether it is awaiting upload, submitted or published. The public Gallery lookup records whether the version is listed. An unavailable lookup remains visible in the report. |

Keep the evidence in a repository Actions artifact or issue comment and reference it by URL. The approval fields carry status, source identity, file digests and links. Store credentials through the provider's configured authentication flow.

## Approve the exact prepared files

The repository owner posts one JSON object in an issue comment. The fields below show the format. Replace the example digests and IDs with the candidate's exact values. Use lower-case hex strings for SHA values. Evidence links can point to this repository's Actions runs or artifacts, or to issue comments containing the evidence.

~~~json
{
  "schema": 1,
  "tag": "v1.12.0",
  "commit": "0123456789abcdef0123456789abcdef01234567",
  "repository": "ProGraMajster/ModernFormsNext",
  "prepareRunId": "123456",
  "manifestSha256": "0000000000000000000000000000000000000000000000000000000000000000",
  "approved": true,
  "visualStudio": {
    "status": "passed",
    "vsixSha256": "0000000000000000000000000000000000000000000000000000000000000000",
    "evidenceUrl": "https://github.com/ProGraMajster/ModernFormsNext/issues/82#issuecomment-1234"
  },
  "android": {
    "status": "physical-passed",
    "apkSha256": "0000000000000000000000000000000000000000000000000000000000000000",
    "matrixUrl": "https://github.com/ProGraMajster/ModernFormsNext/actions/runs/234567/artifacts/3"
  },
  "marketplace": {
    "status": "approved-pending-upload",
    "vsixSha256": "0000000000000000000000000000000000000000000000000000000000000000",
    "evidenceUrl": "https://github.com/ProGraMajster/ModernFormsNext/issues/82#issuecomment-2345"
  }
}
~~~

Use **submitted** or **published** for the Marketplace status once that milestone is observed. To authorize publication, manually dispatch the **Release** workflow on the same tag and supply **prepare_run_id** and **approval_comment_id**. The workflow authenticates the owner comment and successful preparation run, resolves the tag on origin, and verifies the candidate inventory and SHA-256 values. It retains the combined report as **release-publication-evidence**, creates the GitHub Release, then uses NuGet Trusted Publishing. The Marketplace upload is an operator handoff.

## Rehearse without publication

First run **scripts/Test-WorkflowSyntax.ps1** from PowerShell 7 on Windows x64. The normal
required PR job also runs it, including for documentation changes. It validates every workflow
with pinned [actionlint](https://github.com/rhysd/actionlint/releases/tag/v1.7.12), verifies the
downloaded archive's SHA-256, and removes its temporary tool files. YAML structure and GitHub
expression contexts are checked without dispatching a workflow or creating a release tag.
PowerShell bodies still require their dedicated script tests; this is not a publication rehearsal.

The approval JSON path is declared in both consuming publication steps. GitHub permits
the runner context in **steps.env**, but not **jobs.env**; using it at job scope previously
made the whole release workflow invalid before either job could start.
[GitHub context availability](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#context-availability).
Both steps use the same runner temporary file; the owner/provenance checks and subsequent
exact-candidate evidence gates retain their existing ordering and requirements.

In an isolated local clone, build the exact candidate and use a local tag that matches its package version. Build and validate the versioned archives with **-RequireTag**, run **Test-ReleaseDocumentationBrowser.ps1**, and arrange the prepared files as the workflow does. Run **ReleaseEvidence.ps1 -Prepare** to produce the manifest. Run its approval mode with local fixture evidence to exercise positive and negative cases. **scripts/tests/Test-ReleaseHandoff.ps1** uses an isolated Git origin and simulated GitHub replies, and **scripts/tests/Test-ReleaseEvidence.ps1** verifies missing approval, source mismatch and artifact mutation. A rehearsal result records the simulated gates separately from observations made on release hardware.

The GitHub and Marketplace behaviors used by this workflow are described in their documentation. [GitHub Actions artifacts](https://docs.github.com/en/actions/tutorials/store-and-share-data) · [Visual Studio Marketplace](https://learn.microsoft.com/visualstudio/extensibility/walkthrough-publishing-a-visual-studio-extension)
