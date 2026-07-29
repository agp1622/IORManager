import { useCallback, useEffect, useState } from 'react'
import { apiFetch } from '../contexts/AuthContext'

const AccountingSummary = ({ apiBaseUrl, t, formatCurrency, locale }) => {
  const today = new Date()
  const [month, setMonth] = useState(
    `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`,
  )
  const [summary, setSummary] = useState(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState(null)

  const [isrRateInput, setIsrRateInput] = useState('0')
  const [isSavingRate, setIsSavingRate] = useState(false)
  const [rateError, setRateError] = useState(null)

  const loadSummary = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const [yearStr, monthStr] = month.split('-')
      const response = await apiFetch(
        `${apiBaseUrl}/Accounting/summary?year=${yearStr}&month=${monthStr}`,
      )
      if (!response.ok) {
        throw new Error(t('accounting.loadError'))
      }
      const body = await response.json()
      setSummary(body)
    } catch (err) {
      setError(err.message || t('accounting.loadError'))
    } finally {
      setIsLoading(false)
    }
  }, [apiBaseUrl, month, t])

  useEffect(() => {
    let isMounted = true

    const loadSettings = async () => {
      try {
        const response = await apiFetch(`${apiBaseUrl}/Accounting/settings`)
        if (!response.ok) return
        const body = await response.json()
        if (isMounted) {
          setIsrRateInput(String(body.isrRatePercent ?? 0))
        }
      } catch {
        // Ignore — the rate field just stays at its default.
      }
    }

    loadSettings()
    return () => {
      isMounted = false
    }
  }, [apiBaseUrl])

  useEffect(() => {
    loadSummary()
  }, [loadSummary])

  const handleSaveRate = async () => {
    setIsSavingRate(true)
    setRateError(null)
    try {
      const response = await apiFetch(`${apiBaseUrl}/Accounting/settings`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isrRatePercent: Number(isrRateInput) || 0 }),
      })
      if (!response.ok) {
        throw new Error(t('accounting.rateSaveError'))
      }
      await loadSummary()
    } catch (err) {
      setRateError(err.message || t('accounting.rateSaveError'))
    } finally {
      setIsSavingRate(false)
    }
  }

  return (
    <div className="insights">
      <div className="insights__toolbar">
        <label className="toolbar__control">
          {t('accounting.monthLabel')}
          <input
            type="month"
            value={month}
            onChange={(event) => setMonth(event.target.value)}
          />
        </label>
        <label className="toolbar__control">
          {t('accounting.isrRateLabel')}
          <div className="accounting-rate-input">
            <input
              type="number"
              min="0"
              max="100"
              step="0.01"
              value={isrRateInput}
              onChange={(event) => setIsrRateInput(event.target.value)}
            />
            <span>%</span>
            <button
              type="button"
              className="button button--secondary button--compact"
              onClick={handleSaveRate}
              disabled={isSavingRate}
            >
              {isSavingRate ? t('accounting.rateSaving') : t('accounting.rateSave')}
            </button>
          </div>
        </label>
      </div>

      {rateError ? (
        <div className="banner banner--error" role="alert">
          {rateError}
        </div>
      ) : null}

      <p className="insights__hint">{t('accounting.dopOnlyNote')}</p>

      {isLoading ? (
        <p className="muted">{t('accounting.loading')}</p>
      ) : error ? (
        <div className="banner banner--error" role="alert">
          {error}
        </div>
      ) : !summary ? null : (
        <div className="insights__grid">
          <div className="insights__card insights__card--stat">
            <h3>{t('accounting.netIncomeHeading')}</h3>
            <div className="insights__stat-row">
              <div className="insights__stat">
                <span className="insights__stat-label">{t('accounting.revenueLabel')}</span>
                <span className="insights__stat-value">
                  {formatCurrency(summary.revenueSubtotal, 'DOP', locale)}
                </span>
              </div>
              <div className="insights__stat">
                <span className="insights__stat-label">{t('accounting.expensesLabel')}</span>
                <span className="insights__stat-value">
                  {formatCurrency(summary.expensesTotal, 'DOP', locale)}
                </span>
              </div>
              <div className="insights__stat">
                <span className="insights__stat-label">{t('accounting.netIncomeLabel')}</span>
                <span className="insights__stat-value">
                  {formatCurrency(summary.netIncome, 'DOP', locale)}
                </span>
              </div>
            </div>
            <p className="insights__hint">
              {t('accounting.basedOnCounts')
                .replace('{{invoices}}', summary.invoiceCount)
                .replace('{{expenses}}', summary.expenseCount)}
            </p>
          </div>

          <div className="insights__card insights__card--stat">
            <h3>{t('accounting.isrHeading')}</h3>
            <div className="insights__conversion">
              <span className="insights__conversion-rate">
                {formatCurrency(summary.estimatedIsr, 'DOP', locale)}
              </span>
              <span className="insights__stat-sub">
                {t('accounting.isrHint').replace('{{rate}}', summary.isrRatePercent)}
              </span>
            </div>
          </div>

          <div className="insights__card insights__card--wide">
            <h3>{t('accounting.itbisHeading')}</h3>
            <p className="insights__hint">{t('accounting.itbisHint')}</p>
            <div className="insights__stat-row">
              <div className="insights__stat">
                <span className="insights__stat-label">{t('accounting.itbisCollectedLabel')}</span>
                <span className="insights__stat-value">
                  {formatCurrency(summary.itbisCollected, 'DOP', locale)}
                </span>
              </div>
              <div className="insights__stat">
                <span className="insights__stat-label">{t('accounting.itbisPaidLabel')}</span>
                <span className="insights__stat-value">
                  {formatCurrency(summary.itbisPaid, 'DOP', locale)}
                </span>
              </div>
              <div className="insights__stat">
                <span className="insights__stat-label">{t('accounting.itbisPayableLabel')}</span>
                <span className="insights__stat-value">
                  {formatCurrency(summary.itbisPayable, 'DOP', locale)}
                </span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export default AccountingSummary
