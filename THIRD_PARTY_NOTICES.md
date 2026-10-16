# Third-party notices

Dependencies are centrally pinned in Directory.Packages.props and resolved in packages.lock.json. Preserve upstream license files from NuGet packages when redistributing binaries.

- PDFsharp and PDFsharp-MigraDoc 6.2.4: MIT, copyright empira Software GmbH. Both pinned to 6.2.4. https://www.nuget.org/packages/PDFsharp/6.2.4 and https://www.nuget.org/packages/PDFsharp-MigraDoc/6.2.4. Later 6.5+ use different licensing; upgrades require review.
- Embedded Noto Sans Regular/Bold: SIL Open Font License 1.1; full notice in src/Infrastructure/Exporters/Fonts/OFL.txt, copied alongside output. Source https://github.com/notofonts/noto-fonts/tree/main/hinted/ttf/NotoSans. Embedded unmodified.
- MediatR 12.5.0 retains that version's upstream license; no automatic migration to later licensing.
- RazorEngineCore needs Microsoft.CodeAnalysis at runtime; compiler dependencies retained transitively.
- ClosedXML/adapters retain their upstream licenses; review the complete resolved graph before redistribution.

No new source copyright/license is assigned to pre-existing code here. Choose a source license before publishing outside your organization.
