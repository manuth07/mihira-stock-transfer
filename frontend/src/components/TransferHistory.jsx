function formatLocalDateTime(timestampStr) {
  if (!timestampStr) {
    return '—'
  }

  // The backend produces ISO 8601 UTC strings (e.g., 2026-10-27T15:29:08.4091612Z).
  // If the string lacks a timezone specifier, append 'Z' to guarantee UTC interpretation.
  const hasTimeZone = timestampStr.endsWith('Z') || /[+-]\d{2}:\d{2}$/.test(timestampStr)
  const normalizedStr = hasTimeZone ? timestampStr : `${timestampStr}Z`

  const date = new Date(normalizedStr)
  if (Number.isNaN(date.getTime())) {
    return timestampStr
  }

  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(date)
}

export default function TransferHistory({ transfers, isLoading, error }) {
  const hasTransfers = transfers.length > 0

  return (
    <section className="app-section" aria-labelledby="history-heading">
      <div className="section-header">
        <div>
          <h2 id="history-heading" className="section-title">
            Transfer history
          </h2>
          <p className="section-subtitle">
            Audit log of all completed stock movements.
          </p>
        </div>
      </div>

      {error && !hasTransfers && (
        <div className="feedback-banner feedback-error" role="alert">
          <p className="feedback-message">{error}</p>
        </div>
      )}

      {isLoading && !hasTransfers && (
        <div className="state-card" aria-busy="true" aria-live="polite">
          <p>Loading transfer history...</p>
        </div>
      )}

      {!isLoading && !error && !hasTransfers && (
        <div className="state-card">
          <p>No transfers yet.</p>
        </div>
      )}

      {hasTransfers && (
        <div className="table-responsive">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col" className="col-timestamp">
                  Date/time (local)
                </th>
                <th scope="col" className="col-history-product">
                  Product & package size
                </th>
                <th scope="col" className="col-history-branch">
                  From
                </th>
                <th scope="col" className="col-history-branch">
                  To
                </th>
                <th scope="col" className="col-quantity">
                  Quantity
                </th>
              </tr>
            </thead>
            <tbody>
              {transfers.map((item) => (
                <tr key={item.id}>
                  <td className="col-timestamp">
                    <time dateTime={item.createdAtUtc}>
                      {formatLocalDateTime(item.createdAtUtc)}
                    </time>
                  </td>
                  <td className="col-history-product">
                    <span className="product-name">{item.productName}</span>
                    <span className="product-package">{item.packageSize}</span>
                  </td>
                  <td className="col-history-branch">{item.fromBranchName}</td>
                  <td className="col-history-branch">{item.toBranchName}</td>
                  <td className="col-quantity tabular-nums">{item.quantity}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
