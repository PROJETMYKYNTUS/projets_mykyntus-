import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

import { DOCUMENTATION_ROUTES } from './documentation.routes';

@NgModule({
  imports: [RouterModule.forChild(DOCUMENTATION_ROUTES)],
})
export class DocumentationModule {}
