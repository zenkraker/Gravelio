import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';

/**
 * Outlined, rounded fields instead of the filled-with-underline default.
 * Provided per page, not in appConfig: a global import would pull the form-field code into the initial bundle.
 */
export const OUTLINE_FIELDS = {
  provide: MAT_FORM_FIELD_DEFAULT_OPTIONS,
  useValue: { appearance: 'outline' },
};
