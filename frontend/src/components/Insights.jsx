import { useEffect, useMemo, useState } from 'react'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { apiFetch } from '../contexts/AuthContext'
import ThemedSelect from './ThemedSelect'

const PAID_COLOR = '#2fbf85'
const UNPAID_COLOR = '#f0a63a'
const QUOTES_COLOR = '#7dc8ff'
const INVOICES_COLOR = '#ffb347'

const Insights = ({ apiBaseUrl, currency, currencyOptions, onCurrencyChange, t, formatCurrency, locale }) => {
  const [data, setData] = useState(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    let isMounted = true

    const load = async () => {
      setIsLoading(true)
      setError(null)
      try {
        const response = await apiFetch(
          `${apiBaseUrl}/Insights?currency=${encodeURIComponent(currency)}&months=12`,
        )
        if (!response.ok) {
          throw new Error(t('insights.loadError'))
        }
        const body = await response.json()
        if (isMounted) {
          setData(body)
        }
      } catch (err) {
        if (isMounted) {
          setError(err.message || t('insights.loadError'))
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    load()
    return () => {
      isMounted = false
    }
  }, [apiBaseUrl, currency, t])

  const monthlyChartData = useMemo(() => {
    if (!data?.monthly) {
      return []
    }
    return data.monthly.map((entry) => ({
      label: entry.label,
      [t('insights.quotesLabel')]: entry.quotesTotal,
      [t('insights.invoicesLabel')]: entry.invoicesTotal,
    }))
  }, [data, t])

  const paidChartData = useMemo(() => {
    if (!data?.paidBreakdown) {
      return []
    }
    return [
      { name: t('insights.paidLabel'), value: data.paidBreakdown.paidCount, color: PAID_COLOR },
      { name: t('insights.unpaidLabel'), value: data.paidBreakdown.unpaidCount, color: UNPAID_COLOR },
    ].filter((entry) => entry.value > 0)
  }, [data, t])

  const hasAnyData =
    data && (data.monthly.some((m) => m.quotesCount > 0 || m.invoicesCount > 0) || data.conversion.totalQuotes > 0)

  return (
    <div className="insights">
      <div className="insights__toolbar">
        <label className="toolbar__control">
          {t('insights.currencyLabel')}
          <ThemedSelect
            value={currency}
            onChange={onCurrencyChange}
            ariaLabel={t('insights.currencyLabel')}
            options={currencyOptions.map((code) => ({ value: code, label: code }))}
          />
        </label>
      </div>

      {isLoading ? (
        <p className="muted">{t('insights.loading')}</p>
      ) : error ? (
        <div className="banner banner--error" role="alert">
          {error}
        </div>
      ) : !hasAnyData ? (
        <p className="muted">{t('insights.noData')}</p>
      ) : (
        <div className="insights__grid">
          <div className="insights__card insights__card--stat">
            <h3>{t('insights.thisMonthHeading')}</h3>
            <div className="insights__stat-row">
              <div className="insights__stat">
                <span className="insights__stat-label">{t('insights.quotesLabel')}</span>
                <span className="insights__stat-value">{data.currentMonth.quotesCount}</span>
                <span className="insights__stat-sub">
                  {formatCurrency(data.currentMonth.quotesTotal, currency, locale)}
                </span>
              </div>
              <div className="insights__stat">
                <span className="insights__stat-label">{t('insights.invoicesLabel')}</span>
                <span className="insights__stat-value">{data.currentMonth.invoicesCount}</span>
                <span className="insights__stat-sub">
                  {formatCurrency(data.currentMonth.invoicesTotal, currency, locale)}
                </span>
              </div>
            </div>
          </div>

          <div className="insights__card insights__card--stat">
            <h3>{t('insights.conversionHeading')}</h3>
            <div className="insights__conversion">
              <span className="insights__conversion-rate">
                {Math.round((data.conversion.conversionRate || 0) * 100)}%
              </span>
              <span className="insights__stat-sub">
                {t('insights.conversionHint')
                  .replace('{{converted}}', data.conversion.convertedQuotes)
                  .replace('{{total}}', data.conversion.totalQuotes)}
              </span>
            </div>
          </div>

          <div className="insights__card insights__card--wide">
            <h3>{t('insights.monthlyTrendHeading')}</h3>
            <p className="insights__hint">{t('insights.monthlyTrendHint')}</p>
            <div className="insights__chart">
              <ResponsiveContainer width="100%" height={280}>
                <BarChart data={monthlyChartData}>
                  <CartesianGrid strokeDasharray="3 3" opacity={0.2} />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip formatter={(value) => formatCurrency(value, currency, locale)} />
                  <Legend />
                  <Bar dataKey={t('insights.quotesLabel')} fill={QUOTES_COLOR} radius={[4, 4, 0, 0]} />
                  <Bar dataKey={t('insights.invoicesLabel')} fill={INVOICES_COLOR} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="insights__card">
            <h3>{t('insights.paidBreakdownHeading')}</h3>
            {paidChartData.length === 0 ? (
              <p className="muted">{t('insights.noData')}</p>
            ) : (
              <div className="insights__chart">
                <ResponsiveContainer width="100%" height={220}>
                  <PieChart>
                    <Pie
                      data={paidChartData}
                      dataKey="value"
                      nameKey="name"
                      innerRadius={45}
                      outerRadius={80}
                      paddingAngle={2}
                    >
                      {paidChartData.map((entry) => (
                        <Cell key={entry.name} fill={entry.color} />
                      ))}
                    </Pie>
                    <Legend />
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

export default Insights