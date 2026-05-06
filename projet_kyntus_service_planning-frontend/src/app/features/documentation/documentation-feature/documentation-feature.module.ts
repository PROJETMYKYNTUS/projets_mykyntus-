import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

import { DOCUMENTATION_FEATURE_ROUTES } from './documentation-feature.routes';

@NgModule({
  imports: [RouterModule.forChild(DOCUMENTATION_FEATURE_ROUTES)],
})
export class DocumentationFeatureModule {}
