import { BarChart, LineChart, SankeyChart, TreemapChart } from 'echarts/charts'
import { GridComponent, LegendComponent, TooltipComponent } from 'echarts/components'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'

use([CanvasRenderer, TreemapChart, BarChart, LineChart, SankeyChart, GridComponent, LegendComponent, TooltipComponent])

export { default as VChart } from 'vue-echarts'
export type { EChartsOption } from 'echarts'
