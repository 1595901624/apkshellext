<!-- 中英文皆可。Descriptions in English or Chinese are both fine. -->

- [ ] `msbuild ApkShellext2.sln /p:Configuration=Release /p:Platform="Any CPU"` builds with **zero warnings** (the project keeps a zero-warning baseline, see compile_guidance.md §5.3)
- [ ] Manually tested in Explorer (state Windows version; for icon changes attach before/after screenshots)
- [ ] User-visible strings added to all satellite resources, or explicitly marked as English-only
- [ ] Added a line to the upcoming release notes section in README.md / README.zh-CN.md if user-visible
