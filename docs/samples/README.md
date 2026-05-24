# Pack Samples

This folder contains valid, minimal sample packs for local experimentation.

## Contents

- `template-pack/`: sample template pack with `pack.yaml` and valid Scriban templates.
- `rules-pack/`: sample rules pack with `pack.yaml` and valid steering markdown documents.
- `sample-validation.config.yaml`: config file used to validate these samples with `steergen validate`.

## Validation

Run from repository root:

```bash
steergen validate --config docs/samples/sample-validation.config.yaml
```

Expected result: exit code `0` and no validation errors.
