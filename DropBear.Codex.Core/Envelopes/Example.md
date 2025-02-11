```csharp
#region Usage Example

	#region Domain-Specific Example: Payment

	/// <summary>
	/// Represents payment data.
	/// </summary>
	public class PaymentData
	{
		/// <summary>
		/// Gets or sets the payment amount.
		/// </summary>
		public decimal Amount { get; set; }

		/// <summary>
		/// Gets or sets the currency code (e.g. "USD").
		/// </summary>
		public string Currency { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="PaymentData"/> class.
		/// </summary>
		/// <param name="amount">The payment amount.</param>
		/// <param name="currency">The currency code.</param>
		public PaymentData(decimal amount, string currency)
		{
			Amount = amount;
			Currency = currency;
		}
	}

	/// <summary>
	/// A specialized envelope for payment data that enforces domain-specific validations.
	/// </summary>
	public class PaymentEnvelope : Envelope<PaymentData>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PaymentEnvelope"/> class.
		/// </summary>
		/// <param name="paymentData">The payment data payload.</param>
		/// <param name="headers">Optional headers.</param>
		public PaymentEnvelope(PaymentData paymentData, IDictionary<string, object> headers = null)
			: base(paymentData, headers)
		{
			// Register a domain-specific payload validator.
			RegisterPayloadValidator(pd =>
			{
				if (pd == null)
					return new ValidationResult(false, "Payment data cannot be null.");
				if (pd.Amount <= 0)
					return new ValidationResult(false, "Payment amount must be greater than zero.");
				if (string.IsNullOrWhiteSpace(pd.Currency))
					return new ValidationResult(false, "Currency must be provided.");
				return ValidationResult.Success;
			});
		}
	}

	/// <summary>
	/// A sample handler for processing payment envelopes.
	/// </summary>
	public class PaymentHandler
	{
		/// <summary>
		/// Processes the payment envelope by sealing, optionally encrypting, and then logging details.
		/// </summary>
		/// <param name="envelope">The payment envelope to process.</param>
		public void Handle(Envelope<PaymentData> envelope)
		{
			// Seal the envelope if it isn't already sealed.
			if (!envelope.IsSealed)
			{
				// Use a fixed key for demonstration; in production, retrieve securely.
				byte[] key = Encoding.UTF8.GetBytes("SuperSecretKey123!");
				envelope.Seal(key);
			}

			// Optionally encrypt the payload.
			// Uncomment the following lines if encryption is desired.
			// byte[] encryptionKey = Encoding.UTF8.GetBytes("EncryptionKey1234"); // Must be 16/24/32 bytes for AES.
			// envelope.EncryptPayload(encryptionKey);

			// Process the payment.
			Console.WriteLine($"Processing payment of {envelope.Payload.Amount} {envelope.Payload.Currency}");
			Console.WriteLine($"Envelope created at: {envelope.CreatedDate:o}");
			if (envelope.SealedDate.HasValue)
			{
				Console.WriteLine($"Envelope sealed at: {envelope.SealedDate:o}");
			}
			if (!string.IsNullOrEmpty(envelope.Signature))
			{
				Console.WriteLine($"Envelope signature: {envelope.Signature}");
			}
		}
	}

	#endregion

	/// <summary>
	/// Demonstrates encryption and decryption of the envelope payload.
	/// </summary>
	public class EncryptionTest
	{
		/// <summary>
		/// Runs the encryption test.
		/// </summary>
		public static void Run()
		{
			try
			{
				// Create sample payment data.
				PaymentData payment = new PaymentData(amount: 150.00m, currency: "USD");

				// Create initial headers.
				var headers = new Dictionary<string, object>
				{
					{ "TransactionID", Guid.NewGuid() },
					{ "SourceSystem", "CheckoutService" }
				};

				// Create a PaymentEnvelope with the given payment and headers.
				PaymentEnvelope envelope = new PaymentEnvelope(payment, headers);
				envelope.AddHeader("InitiatedBy", "EncryptionTest");

				// Seal the envelope (using a key for HMAC signature, if desired).
				// Note: The key used for signing here is for demonstration purposes.
				byte[] signingKey = Encoding.UTF8.GetBytes("1234567890123456"); // 16-byte key for AES/HMAC demonstration.
				envelope.Seal(signingKey);

				// --- Activate Encryption ---
				// Use a symmetric key for encryption. Ensure the key is 16, 24, or 32 bytes for AES.
				byte[] encryptionKey = Encoding.UTF8.GetBytes("abcdefghijklmnop"); // 16-byte key
				envelope.EncryptPayload(encryptionKey);
				Console.WriteLine("Payload has been encrypted.");

				// Attempting to access the payload while it is encrypted will throw an exception.
				try
				{
					var payload = envelope.Payload;
				}
				catch (Exception ex)
				{
					Console.WriteLine("Expected error accessing encrypted payload: " + ex.Message);
				}

				// --- Decrypt the Payload ---
				envelope.DecryptPayload(encryptionKey);
				Console.WriteLine("Payload has been decrypted.");
				Console.WriteLine($"Decrypted Payload: {envelope.Payload.Amount} {envelope.Payload.Currency}");
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Encryption test failed: {ex.Message}");
			}
		}
	}

	/// <summary>
	/// Demonstrates usage of the upgraded envelope library.
	/// </summary>
	public class Program
	{
		public static void Main()
		{
			try
			{
				// Create payment data.
				PaymentData payment = new PaymentData(amount: 150.00m, currency: "USD");

				// Initial headers.
				var headers = new Dictionary<string, object>
				{
					{ "TransactionID", Guid.NewGuid() },
					{ "SourceSystem", "CheckoutService" }
				};

				// Optional: Add a header validator to enforce that header keys start with an uppercase letter.
				Func<string, object, ValidationResult> headerValidator = (key, value) =>
					char.IsUpper(key[0])
						? ValidationResult.Success
						: new ValidationResult(false, "Header key must start with an uppercase letter.");

				// Create a PaymentEnvelope with the header validator.
				PaymentEnvelope envelope = new PaymentEnvelope(payment, headers);
				envelope.RegisterHeaderValidator(headerValidator);

				// Optionally, add another header.
				envelope.AddHeader("InitiatedBy", "OnlinePortal");

				// Subscribe to events.
				envelope.Sealed += (sender, args) => Console.WriteLine("Envelope sealed event fired.");
				envelope.Unsealed += (sender, args) => Console.WriteLine("Envelope unsealed event fired.");
				envelope.PayloadModified += (sender, args) => Console.WriteLine("Envelope payload modified event fired.");

				// Process the envelope with the handler.
				PaymentHandler handler = new PaymentHandler();
				handler.Handle(envelope);

				// Serialize the envelope.
				string serialized = envelope.ToSerializedString();
				Console.WriteLine("\nSerialized Envelope:");
				Console.WriteLine(serialized);

				// Deserialize the envelope.
				Envelope<PaymentData> deserialized = Envelope<PaymentData>.FromSerializedString(serialized);
				Console.WriteLine("\nDeserialized Envelope:");
				Console.WriteLine($"Payload: {deserialized.Payload.Amount} {deserialized.Payload.Currency}");
				Console.WriteLine($"Is Sealed: {deserialized.IsSealed}");
				Console.WriteLine($"Signature: {deserialized.Signature}");

				// Demonstrate cloning.
				var clonedEnvelope = envelope.Clone();
				Console.WriteLine("\nCloned Envelope (unsealed):");
				Console.WriteLine($"Payload: {clonedEnvelope.Payload.Amount} {clonedEnvelope.Payload.Currency}");
				Console.WriteLine($"Is Sealed: {clonedEnvelope.IsSealed}");

				// Demonstrate composite envelope for batch processing.
				var payments = new List<PaymentData>
				{
					new PaymentData(100.00m, "USD"),
					new PaymentData(200.00m, "EUR")
				};
				CompositeEnvelope<PaymentData> compositeEnvelope = new CompositeEnvelope<PaymentData>(payments);
				Console.WriteLine("\nComposite Envelope created with {0} payment(s).", compositeEnvelope.Payload.Count);


				// Run the encryption test.
				EncryptionTest.Run();
			}
			catch (Exception ex)
			{
				// Log errors appropriately.
				Console.Error.WriteLine($"An error occurred: {ex.Message}");
			}
		}
	}

	#endregion
```
