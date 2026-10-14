import { Directive, input } from '@angular/core';

@Directive({
  selector: '[tcTooltip]',
  host: {
    '[attr.aria-label]': 'tcTooltip()',
    '[attr.data-tooltip]': 'tcTooltip()',
  },
})
export class TooltipDirective {
  readonly tcTooltip = input.required<string>();
}
